import { ComponentFixture, TestBed } from '@angular/core/testing';
import { afterEach, describe, it, expect, beforeEach, vi } from 'vitest';
import { provideAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { CreateOrEditGroupModalComponent } from './create-group-modal.component';
import { DashboardService } from '../dashboard.service';

function buildMockDashboardService() {
  return {
    isGroupNameAvailable: vi.fn().mockReturnValue(of(true)),
    createGroup: vi.fn().mockReturnValue(
      of({ id: 'new-id', name: 'Test Group', description: null }),
    ),
    updateGroup: vi.fn().mockReturnValue(
      of({ id: 'existing-id', name: 'Updated Group', description: null }),
    ),
  };
}

// jsdom does not implement the <dialog> methods the modal calls
function stubDialogMethods(): void {
  HTMLDialogElement.prototype.showModal = function (this: HTMLDialogElement) { this.open = true; };
  HTMLDialogElement.prototype.close = function (this: HTMLDialogElement) { this.open = false; };
}

describe('CreateOrEditGroupModalComponent', () => {
  let component: CreateOrEditGroupModalComponent;
  let fixture: ComponentFixture<CreateOrEditGroupModalComponent>;
  let mockService: ReturnType<typeof buildMockDashboardService>;

  beforeEach(async () => {
    stubDialogMethods();
    mockService = buildMockDashboardService();

    await TestBed.configureTestingModule({
      imports: [CreateOrEditGroupModalComponent],
      providers: [
        provideAnimations(),
        { provide: DashboardService, useValue: mockService },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(CreateOrEditGroupModalComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('open', false);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('create mode', () => {
    it('renders with empty fields and "Create new group" title', () => {
      fixture.componentRef.setInput('open', true);
      fixture.detectChanges();

      expect(component.title()).toBe('Create new group');
      expect(component.form.controls.name.value).toBe('');
      expect(component.form.controls.description.value).toBeNull();
    });

    it('submit is disabled when name is empty', () => {
      fixture.componentRef.setInput('open', true);
      fixture.detectChanges();

      expect(component.form.invalid).toBe(true);
    });
  });

  describe('edit mode', () => {
    const initial = { id: 'existing-id', name: 'My Group', description: 'Some desc' };

    beforeEach(() => {
      fixture.componentRef.setInput('mode', 'edit');
      fixture.componentRef.setInput('initialValue', initial);
      fixture.componentRef.setInput('open', true);
      fixture.detectChanges();
    });

    it('pre-fills form with initialValue', () => {
      expect(component.form.controls.name.value).toBe('My Group');
      expect(component.form.controls.description.value).toBe('Some desc');
    });

    it('form is pristine after being pre-filled', () => {
      expect(component.form.pristine).toBe(true);
    });
  });

  describe('submit behaviour', () => {
    afterEach(() => vi.useRealTimers());

    it('does not call createGroup when form is invalid', async () => {
      fixture.componentRef.setInput('open', true);
      fixture.detectChanges();

      // name is empty → form.invalid
      await component.onSubmit();

      expect(mockService.createGroup).not.toHaveBeenCalled();
    });

    it('calls createGroup and emits submitted on success', async () => {
      vi.useFakeTimers();
      fixture.componentRef.setInput('open', true);
      fixture.detectChanges();

      const emitted: unknown[] = [];
      component.submitted.subscribe(v => emitted.push(v));

      component.form.controls.name.setValue('Test Group');
      component.form.controls.name.markAsDirty();
      component.form.controls.name.markAsTouched();
      // advance past the debounce so the async name validator (mocked: available) settles
      await vi.advanceTimersByTimeAsync(400);
      fixture.detectChanges();

      await component.onSubmit();
      fixture.detectChanges();

      expect(mockService.createGroup).toHaveBeenCalledWith({
        name: 'Test Group',
        description: null,
      });
      expect(emitted.length).toBe(1);
    });
  });

  describe('async name validator', () => {
    afterEach(() => vi.useRealTimers());

    it('marks name control with { taken: true } when name is not available', async () => {
      vi.useFakeTimers();
      mockService.isGroupNameAvailable.mockReturnValue(of(false));

      fixture.componentRef.setInput('open', true);
      fixture.detectChanges();

      const nameControl = component.form.controls.name;
      nameControl.setValue('Taken Name');
      nameControl.markAsDirty();
      nameControl.markAsTouched();

      // trigger blur update
      nameControl.updateValueAndValidity();

      // advance past debounce timer
      await vi.advanceTimersByTimeAsync(400);
      fixture.detectChanges();

      expect(nameControl.errors?.['taken']).toBe(true);
    });
  });
});
