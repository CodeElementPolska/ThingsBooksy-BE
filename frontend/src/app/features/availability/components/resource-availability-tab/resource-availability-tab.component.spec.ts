import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { ResourceAvailabilityTabComponent } from './resource-availability-tab.component';
import { AvailabilityService } from '../../availability.service';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { NotificationService } from '../../../../shared/services/notification.service';
import {
  AvailabilityRule,
  AvailabilityRulesViewModel,
} from '../../models/availability.model';

const mockRule: AvailabilityRule = {
  ruleId: 'rule-1',
  ruleType: 'RECURRING',
  ruleMode: 'AVAILABLE',
  daysOfWeek: [1, 2, 3],
  startTime: '09:00',
  endTime: '17:00',
  endDayOffset: 0,
  startDate: null,
  endDate: null,
};

const mockInheritedRule: AvailabilityRule = {
  ruleId: 'inherited-1',
  ruleType: 'RECURRING',
  ruleMode: 'AVAILABLE',
  daysOfWeek: [1, 2, 3, 4, 5],
  startTime: '08:00',
  endTime: '18:00',
  endDayOffset: 0,
  startDate: null,
  endDate: null,
};

const mockViewModel: AvailabilityRulesViewModel = {
  bufferMinutes: 15,
  rules: [mockRule],
  inheritedRules: [mockInheritedRule],
};

describe('ResourceAvailabilityTabComponent', () => {
  let component: ResourceAvailabilityTabComponent;
  let fixture: ComponentFixture<ResourceAvailabilityTabComponent>;

  const mockAvailabilityService = {
    getResourceRules: vi.fn(),
    upsertResourceRules: vi.fn(),
  };

  const mockConfirmDialog = {
    confirm: vi.fn(),
    state: signal(null),
    onConfirm: vi.fn(),
    onCancel: vi.fn(),
  };

  const mockNotifications = {
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
    dismiss: vi.fn(),
    toasts: signal([]),
  };

  beforeEach(async () => {
    vi.clearAllMocks();
    mockAvailabilityService.getResourceRules.mockReturnValue(of(mockViewModel));
    mockAvailabilityService.upsertResourceRules.mockReturnValue(of(void 0));
    mockConfirmDialog.confirm.mockResolvedValue(false);

    await TestBed.configureTestingModule({
      imports: [ResourceAvailabilityTabComponent],
      providers: [
        { provide: AvailabilityService, useValue: mockAvailabilityService },
        { provide: ConfirmDialogService, useValue: mockConfirmDialog },
        { provide: NotificationService, useValue: mockNotifications },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ResourceAvailabilityTabComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('resourceId', 'res-123');
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load rules on init and populate signals', () => {
    expect(mockAvailabilityService.getResourceRules).toHaveBeenCalledWith('res-123');
    expect(component.rules()).toHaveLength(1);
    expect(component.rules()[0].ruleId).toBe('rule-1');
  });

  it('should populate inheritedRules signal from service response', () => {
    expect(component.inheritedRules()).toHaveLength(1);
    expect(component.inheritedRules()![0].ruleId).toBe('inherited-1');
  });

  it('should set effectiveBufferMinutes from viewModel', () => {
    expect(component.effectiveBufferMinutes()).toBe(15);
  });

  it('should set isLoading to false after successful load', () => {
    expect(component.isLoading()).toBe(false);
  });

  it('should set error signal on load failure', () => {
    mockAvailabilityService.getResourceRules.mockReturnValue(
      throwError(() => new Error('Network error')),
    );

    const fixture2 = TestBed.createComponent(ResourceAvailabilityTabComponent);
    const comp2 = fixture2.componentInstance;
    fixture2.componentRef.setInput('resourceId', 'res-fail');
    fixture2.detectChanges();

    expect(comp2.error()).toBe('Failed to load availability rules.');
    expect(comp2.isLoading()).toBe(false);
  });

  it('should add a rule when onAddRule is called with a valid form', () => {
    component.addRuleForm.setValue({
      ruleMode: 'AVAILABLE',
      daysOfWeek: [1, 5],
      startTime: '10:00',
      endTime: '12:00',
    });

    const initialCount = component.rules().length;
    component.onAddRule();

    expect(component.rules()).toHaveLength(initialCount + 1);
  });

  it('should not add a rule when form is invalid', () => {
    const initialCount = component.rules().length;
    component.onAddRule();

    expect(component.rules()).toHaveLength(initialCount);
  });

  it('should remove a rule when onRemoveRule is called', () => {
    expect(component.rules()).toHaveLength(1);
    component.onRemoveRule('rule-1');
    expect(component.rules()).toHaveLength(0);
  });

  it('should call upsertResourceRules on save with current rules', async () => {
    await component.onSave();

    expect(mockAvailabilityService.upsertResourceRules).toHaveBeenCalledWith(
      'res-123',
      expect.objectContaining({ rules: expect.any(Array) }),
    );
  });

  it('should set saveSuccess to true after successful save', async () => {
    await component.onSave();

    expect(component.saveSuccess()).toBe(true);
    expect(component.isSaving()).toBe(false);
  });

  it('should set conflictError on 400 conflict error during save', async () => {
    const conflictErr = { type: 'conflict', detail: 'Rule overlap detected.' };
    mockAvailabilityService.upsertResourceRules.mockReturnValue(
      throwError(() => conflictErr),
    );

    await component.onSave();

    expect(component.conflictError()).toBe('Rule overlap detected.');
    expect(component.saveSuccess()).toBe(false);
  });

  it('should set error signal on generic save failure', async () => {
    mockAvailabilityService.upsertResourceRules.mockReturnValue(
      throwError(() => new Error('Server error')),
    );

    await component.onSave();

    expect(component.error()).toBe(
      'Failed to save availability rules. Please try again.',
    );
    expect(component.isSaving()).toBe(false);
  });

  it('should not call upsertResourceRules on reset if user cancels confirmation', async () => {
    mockConfirmDialog.confirm.mockResolvedValue(false);

    await component.onResetToDefaults();

    expect(mockAvailabilityService.upsertResourceRules).not.toHaveBeenCalled();
  });

  it('should call upsertResourceRules with empty rules on reset confirmation', async () => {
    mockConfirmDialog.confirm.mockResolvedValue(true);

    await component.onResetToDefaults();

    expect(mockAvailabilityService.upsertResourceRules).toHaveBeenCalledWith(
      'res-123',
      { bufferMinutes: 0, rules: [] },
    );
  });

  it('should reload rules after successful reset', async () => {
    mockConfirmDialog.confirm.mockResolvedValue(true);
    const callsBefore = mockAvailabilityService.getResourceRules.mock.calls.length;

    await component.onResetToDefaults();

    expect(mockAvailabilityService.getResourceRules.mock.calls.length).toBeGreaterThan(callsBefore);
  });

  it('should format daysOfWeek correctly', () => {
    expect(component.formatDays([1, 2, 3])).toBe('Mon, Tue, Wed');
    expect(component.formatDays(null)).toBe('—');
    expect(component.formatDays([])).toBe('—');
    expect(component.formatDays([0])).toBe('Sun');
  });

  it('should toggle a day on/off in the form', () => {
    component.addRuleForm.patchValue({ daysOfWeek: [1, 3] });

    component.onDayToggle(2); // add Wednesday
    expect((component.addRuleForm.get('daysOfWeek')!.value as number[]).includes(2)).toBe(true);

    component.onDayToggle(1); // remove Monday
    expect((component.addRuleForm.get('daysOfWeek')!.value as number[]).includes(1)).toBe(false);
  });

  it('should switch to ONE_OFF form fields when rule type changes', () => {
    component.selectedRuleType.set('ONE_OFF');
    fixture.detectChanges();

    expect(component.isRecurring()).toBe(false);
    expect(component.addRuleForm.contains('startDate')).toBe(true);
    expect(component.addRuleForm.contains('daysOfWeek')).toBe(false);
  });
});
