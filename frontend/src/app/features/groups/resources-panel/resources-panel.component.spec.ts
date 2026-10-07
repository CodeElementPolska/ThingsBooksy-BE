import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { ResourcesPanelComponent } from './resources-panel.component';
import { ThingsBooksyModulesResourcesCoreFeaturesGetResourceInstancesResourceInstanceRowDto as ResourceRowDto } from '../../../api/data-contracts';
import { SchemaSummary } from '../group-context.store';

const mockResources: ResourceRowDto[] = [
  {
    id: 'res-1',
    resourceTypeId: 'schema-a',
    name: 'Laptop Dell',
    createdAt: '2025-01-01T00:00:00Z',
  },
  {
    id: 'res-2',
    resourceTypeId: 'schema-b',
    name: 'Conference Room A',
    createdAt: '2025-01-02T00:00:00Z',
  },
];

const mockSchemas: SchemaSummary[] = [
  { id: 'schema-a', name: 'Laptops', description: null, propertyDefinitionsCount: 1, propertyDefinitions: [], resourceCount: 0 },
  { id: 'schema-b', name: 'Rooms', description: null, propertyDefinitionsCount: 1, propertyDefinitions: [], resourceCount: 0 },
];

describe('ResourcesPanelComponent', () => {
  let component: ResourcesPanelComponent;
  let fixture: ComponentFixture<ResourcesPanelComponent>;
  let originalIntersectionObserver: typeof IntersectionObserver;

  beforeEach(async () => {
    originalIntersectionObserver = window.IntersectionObserver;

    const mockObserver = vi.fn().mockImplementation(function () {
      return { observe: vi.fn(), unobserve: vi.fn(), disconnect: vi.fn() };
    });
    (window as any).IntersectionObserver = mockObserver;

    await TestBed.configureTestingModule({
      imports: [ResourcesPanelComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(ResourcesPanelComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    (window as any).IntersectionObserver = originalIntersectionObserver;
  });

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  describe('row rendering with schema name mapping', () => {
    it('renders a table row for each resource and maps resourceTypeId to schema name', () => {
      fixture.componentRef.setInput('resources', mockResources);
      fixture.componentRef.setInput('schemas', mockSchemas);
      fixture.detectChanges();

      const rows = fixture.nativeElement.querySelectorAll('tbody tr');
      expect(rows).toHaveLength(2);

      const firstRowCells = rows[0].querySelectorAll('td');
      expect(firstRowCells[0].textContent?.trim()).toBe('Laptop Dell');
      expect(firstRowCells[1].textContent?.trim()).toBe('Laptops');

      const secondRowCells = rows[1].querySelectorAll('td');
      expect(secondRowCells[0].textContent?.trim()).toBe('Conference Room A');
      expect(secondRowCells[1].textContent?.trim()).toBe('Rooms');
    });

    it('renders "—" for type when resourceTypeId does not match any schema', () => {
      const resources: ResourceRowDto[] = [
        { id: 'res-x', resourceTypeId: 'unknown-schema', name: 'Orphan' },
      ];
      fixture.componentRef.setInput('resources', resources);
      fixture.componentRef.setInput('schemas', mockSchemas);
      fixture.detectChanges();

      const cells = fixture.nativeElement.querySelectorAll('tbody td');
      expect(cells[1].textContent?.trim()).toBe('—');
    });
  });

  describe('empty state', () => {
    it('shows empty state message when resources list is empty', () => {
      fixture.componentRef.setInput('resources', []);
      fixture.detectChanges();

      const empty = fixture.nativeElement.querySelector('.resources-panel__empty');
      expect(empty).not.toBeNull();
      expect(empty.textContent?.trim()).toContain('No resources yet');
    });

    it('hides the table when resources list is empty', () => {
      fixture.componentRef.setInput('resources', []);
      fixture.detectChanges();

      const table = fixture.nativeElement.querySelector('table');
      expect(table).toBeNull();
    });
  });

  describe('isOwner input', () => {
    it('hides the Add resource button when isOwner is false', () => {
      fixture.componentRef.setInput('resources', mockResources);
      fixture.componentRef.setInput('schemas', mockSchemas);
      fixture.componentRef.setInput('isOwner', false);
      fixture.detectChanges();

      const btn = fixture.nativeElement.querySelector('.resources-panel__add-btn');
      expect(btn).toBeNull();
    });

    it('shows the Add resource button when isOwner is true', () => {
      fixture.componentRef.setInput('resources', mockResources);
      fixture.componentRef.setInput('schemas', mockSchemas);
      fixture.componentRef.setInput('isOwner', true);
      fixture.detectChanges();

      const btn = fixture.nativeElement.querySelector('.resources-panel__add-btn');
      expect(btn).not.toBeNull();
    });

    it('emits addResource when Add resource button is clicked', () => {
      fixture.componentRef.setInput('resources', mockResources);
      fixture.componentRef.setInput('schemas', mockSchemas);
      fixture.componentRef.setInput('isOwner', true);
      fixture.detectChanges();

      const emitted: void[] = [];
      component.addResource.subscribe(() => emitted.push(undefined));

      const btn = fixture.nativeElement.querySelector('.resources-panel__add-btn');
      btn.click();

      expect(emitted).toHaveLength(1);
    });
  });

  describe('infinite scroll', () => {
    it('emits loadMore when IntersectionObserver fires for the sentinel', () => {
      let intersectCallback = null as IntersectionObserverCallback | null;

      const mockObserver = vi.fn().mockImplementation(function (cb: IntersectionObserverCallback) {
        intersectCallback = cb;
        return {
          observe: vi.fn(),
          unobserve: vi.fn(),
          disconnect: vi.fn(),
        };
      });
      (window as any).IntersectionObserver = mockObserver;

      fixture.componentRef.setInput('resources', mockResources);
      fixture.componentRef.setInput('schemas', mockSchemas);
      fixture.componentRef.setInput('nextCursor', 'cursor-abc');
      fixture.componentRef.setInput('loadingMore', false);
      fixture.detectChanges();

      const emitted: void[] = [];
      component.loadMore.subscribe(() => emitted.push(undefined));

      if (intersectCallback) {
        const fakeEntry = [{ isIntersecting: true }] as unknown as IntersectionObserverEntry[];
        intersectCallback(fakeEntry, {} as IntersectionObserver);
      }

      expect(emitted).toHaveLength(1);
    });
  });
});
