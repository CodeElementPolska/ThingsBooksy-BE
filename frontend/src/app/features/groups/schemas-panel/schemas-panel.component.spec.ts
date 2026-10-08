import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { describe, it, expect, beforeEach } from 'vitest';
import { SchemasPanelComponent } from './schemas-panel.component';
import { SchemaSummary } from '../group-context.store';

const MOCK_SCHEMAS: SchemaSummary[] = [
  { id: 'schema-1', name: 'Equipment', description: 'Physical equipment', propertyDefinitionsCount: 5, propertyDefinitions: [], resourceCount: 0 },
  { id: 'schema-2', name: 'Room', description: null, propertyDefinitionsCount: 2, propertyDefinitions: [], resourceCount: 0 },
];

describe('SchemasPanelComponent', () => {
  let component: SchemasPanelComponent;
  let fixture: ComponentFixture<SchemasPanelComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SchemasPanelComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(SchemasPanelComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  describe('populated state', () => {
    beforeEach(() => {
      fixture.componentRef.setInput('schemas', MOCK_SCHEMAS);
      fixture.componentRef.setInput('isOwner', false);
      fixture.detectChanges();
    });

    it('should render a row for each schema', () => {
      const rows = fixture.debugElement.queryAll(By.css('.schemas-panel__row'));
      expect(rows.length).toBe(2);
    });

    it('should display schema names in rows', () => {
      const names = fixture.debugElement.queryAll(By.css('.schemas-panel__name'));
      expect(names[0].nativeElement.textContent.trim()).toBe('Equipment');
      expect(names[1].nativeElement.textContent.trim()).toBe('Room');
    });

    it('should set aria-label on each row button', () => {
      const rows = fixture.debugElement.queryAll(By.css('.schemas-panel__row'));
      expect(rows[0].nativeElement.getAttribute('aria-label')).toBe('Open schema Equipment');
      expect(rows[1].nativeElement.getAttribute('aria-label')).toBe('Open schema Room');
    });

    it('should emit selectSchema with schema id when row is clicked', () => {
      const emitted: string[] = [];
      component.selectSchema.subscribe((id: string) => emitted.push(id));

      const rows = fixture.debugElement.queryAll(By.css('.schemas-panel__row'));
      rows[0].nativeElement.click();

      expect(emitted).toEqual(['schema-1']);
    });

    it('should emit selectSchema with correct id for second row', () => {
      const emitted: string[] = [];
      component.selectSchema.subscribe((id: string) => emitted.push(id));

      const rows = fixture.debugElement.queryAll(By.css('.schemas-panel__row'));
      rows[1].nativeElement.click();

      expect(emitted).toEqual(['schema-2']);
    });
  });

  describe('empty state', () => {
    beforeEach(() => {
      fixture.componentRef.setInput('schemas', []);
      fixture.componentRef.setInput('isOwner', false);
      fixture.detectChanges();
    });

    it('should show empty state when schemas list is empty', () => {
      const empty = fixture.debugElement.query(By.css('.schemas-panel__empty'));
      expect(empty).toBeTruthy();
    });

    it('should not render any schema rows in empty state', () => {
      const rows = fixture.debugElement.queryAll(By.css('.schemas-panel__row'));
      expect(rows.length).toBe(0);
    });

    it('should not show Add schema CTA in empty state when not owner', () => {
      const cta = fixture.debugElement.query(By.css('.schemas-panel__empty-cta'));
      expect(cta).toBeNull();
    });
  });

  describe('schema wording (story 016)', () => {
    const OLD_EMPTY_STATE = 'Create the first schema to define a resource' + ' type.';
    const NEW_EMPTY_STATE = 'Create the first schema to describe your resources.';

    it('[AC-8] empty state reads "Create the first schema to describe your resources."', () => {
      const texts: string[] = [];
      for (const isOwner of [true, false]) {
        const f = TestBed.createComponent(SchemasPanelComponent);
        f.componentRef.setInput('schemas', []);
        f.componentRef.setInput('isOwner', isOwner);
        f.detectChanges();
        texts.push((f.nativeElement as HTMLElement).textContent?.replace(/\s+/g, ' ') ?? '');
      }

      expect(texts.some((t) => t.includes(NEW_EMPTY_STATE))).toBe(true);
      expect(texts.some((t) => t.includes(OLD_EMPTY_STATE))).toBe(false);
    });

    it('[AC-8] add-resource buttons are labelled "Add resource to schema <name>"', () => {
      fixture.componentRef.setInput('schemas', MOCK_SCHEMAS);
      fixture.componentRef.setInput('isOwner', true);
      fixture.detectChanges();

      const labels = Array.from(
        (fixture.nativeElement as HTMLElement).querySelectorAll('[aria-label^="Add resource"]'),
      ).map((el) => el.getAttribute('aria-label'));

      expect(labels).toEqual(['Add resource to schema Equipment', 'Add resource to schema Room']);
    });
  });

  describe('owner visibility', () => {
    it('should hide the header Add schema button when isOwner is false', () => {
      fixture.componentRef.setInput('schemas', MOCK_SCHEMAS);
      fixture.componentRef.setInput('isOwner', false);
      fixture.detectChanges();

      const btn = fixture.debugElement.query(By.css('.schemas-panel__add-btn'));
      expect(btn).toBeNull();
    });

    it('should show the header Add schema button when isOwner is true', () => {
      fixture.componentRef.setInput('schemas', MOCK_SCHEMAS);
      fixture.componentRef.setInput('isOwner', true);
      fixture.detectChanges();

      const btn = fixture.debugElement.query(By.css('.schemas-panel__add-btn'));
      expect(btn).toBeTruthy();
    });

    it('should show empty-state CTA when isOwner is true and list is empty', () => {
      fixture.componentRef.setInput('schemas', []);
      fixture.componentRef.setInput('isOwner', true);
      fixture.detectChanges();

      const cta = fixture.debugElement.query(By.css('.schemas-panel__empty-cta'));
      expect(cta).toBeTruthy();
    });

    it('should emit addSchema when header button is clicked by owner', () => {
      fixture.componentRef.setInput('schemas', MOCK_SCHEMAS);
      fixture.componentRef.setInput('isOwner', true);
      fixture.detectChanges();

      let emitted = false;
      component.addSchema.subscribe(() => (emitted = true));

      const btn = fixture.debugElement.query(By.css('.schemas-panel__add-btn'));
      btn.nativeElement.click();

      expect(emitted).toBe(true);
    });
  });
});
