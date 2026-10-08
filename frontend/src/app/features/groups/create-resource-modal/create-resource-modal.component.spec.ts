import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { CreateResourceModalComponent } from './create-resource-modal.component';
import { GroupContextStore, SchemaSummary } from '../group-context.store';

/**
 * Story 016, AC-8 — the "creates an instance of it" step of the journey, driven through the screen
 * (review finding trace-auditor-1-2): the create-resource modal submits the schema the user chose,
 * and the request that reaches POST /resources/instances carries it as `resourceSchemaId`.
 */

/** Name of the schema reference before story 016 (concatenated: the old name must not appear in frontend/src). */
const LEGACY_SCHEMA_REFERENCE = 'resource' + 'TypeId';

const GROUP_ID = '11111111-1111-7111-8111-111111111111';
const KAYAK_SCHEMA_ID = '22222222-2222-7222-8222-222222222222';
const ROOM_SCHEMA_ID = '44444444-4444-7444-8444-444444444444';
const CREATED_ID = '33333333-3333-7333-8333-333333333333';

const SCHEMAS: SchemaSummary[] = [
  { id: KAYAK_SCHEMA_ID, name: 'Kayak', description: null, propertyDefinitionsCount: 0, propertyDefinitions: [], resourceCount: 0 },
  { id: ROOM_SCHEMA_ID, name: 'Room', description: null, propertyDefinitionsCount: 0, propertyDefinitions: [], resourceCount: 0 },
];

// jsdom does not implement the <dialog> methods the modal calls
function stubDialogMethods(): void {
  HTMLDialogElement.prototype.showModal = function (this: HTMLDialogElement) { this.open = true; };
  HTMLDialogElement.prototype.close = function (this: HTMLDialogElement) { this.open = false; };
}

describe('CreateResourceModalComponent (story 016)', () => {
  let fixture: ComponentFixture<CreateResourceModalComponent>;
  let http: HttpTestingController;
  let created: { id: string; resourceSchemaId: string; name: string }[];

  beforeEach(async () => {
    stubDialogMethods();

    await TestBed.configureTestingModule({
      imports: [CreateResourceModalComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: GroupContextStore, useValue: { schemas: signal(SCHEMAS).asReadonly() } },
      ],
    }).compileComponents();

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(CreateResourceModalComponent);
    created = [];
    fixture.componentInstance.created.subscribe((event) => created.push(event));
  });

  afterEach(() => {
    http.verify();
  });

  function root(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function typeName(value: string): void {
    const input = root().querySelector<HTMLInputElement>('#resource-name');
    expect(input).not.toBeNull();
    input!.value = value;
    input!.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  /** Lets the async submit handler finish after the response is flushed (one macrotask drains its promise chain). */
  async function settle(): Promise<void> {
    await fixture.whenStable();
    await new Promise<void>((resolve) => setTimeout(resolve));
  }

  function clickCreate(): void {
    const submit = root().querySelector<HTMLButtonElement>('button[type="submit"]');
    expect(submit).not.toBeNull();
    expect(submit!.disabled).toBe(false);
    submit!.click();
  }

  it('[AC-8] submitting the form for a picked schema posts its id as resourceSchemaId to POST /resources/instances', async () => {
    // Arrange — the modal is open without a preselected schema; the user picks "Room" and names the resource
    fixture.componentRef.setInput('groupId', GROUP_ID);
    fixture.componentRef.setInput('open', true);
    fixture.detectChanges();

    const select = root().querySelector<HTMLSelectElement>('#resource-schema');
    expect(select).not.toBeNull();
    select!.value = ROOM_SCHEMA_ID;
    select!.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    typeName('Meeting room 1');

    // Act
    clickCreate();

    // Assert — the request carries the picked schema under the new name only
    const req = http.expectOne((r) => r.method === 'POST' && r.url.endsWith('/resources/instances'));
    const body = req.request.body as Record<string, unknown>;
    expect(body['resourceSchemaId']).toBe(ROOM_SCHEMA_ID);
    expect(body['name']).toBe('Meeting room 1');
    expect(Object.keys(body)).not.toContain(LEGACY_SCHEMA_REFERENCE);

    req.flush({ id: CREATED_ID });
    await settle();

    expect(created).toEqual([{ id: CREATED_ID, resourceSchemaId: ROOM_SCHEMA_ID, name: 'Meeting room 1' }]);
  });

  it('[AC-8] submitting the form opened from "Add resource to schema <name>" posts the preselected schema as resourceSchemaId', async () => {
    // Arrange — opened for the "Kayak" schema (the schemas-panel row action)
    fixture.componentRef.setInput('groupId', GROUP_ID);
    fixture.componentRef.setInput('preselectedSchemaId', KAYAK_SCHEMA_ID);
    fixture.componentRef.setInput('open', true);
    fixture.detectChanges();
    typeName('Kayak 1');

    // Act
    clickCreate();

    // Assert
    const req = http.expectOne((r) => r.method === 'POST' && r.url.endsWith('/resources/instances'));
    const body = req.request.body as Record<string, unknown>;
    expect(body['resourceSchemaId']).toBe(KAYAK_SCHEMA_ID);
    expect(Object.keys(body)).not.toContain(LEGACY_SCHEMA_REFERENCE);

    req.flush({ id: CREATED_ID });
    await settle();

    expect(created).toEqual([{ id: CREATED_ID, resourceSchemaId: KAYAK_SCHEMA_ID, name: 'Kayak 1' }]);
  });
});
