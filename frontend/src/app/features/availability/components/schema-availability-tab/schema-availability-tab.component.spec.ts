import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { of, throwError } from 'rxjs';
import { AvailabilityService } from '../../availability.service';
import { AvailabilityRulesViewModel } from '../../models/availability.model';
import { SchemaAvailabilityTabComponent } from './schema-availability-tab.component';

const mockRulesViewModel: AvailabilityRulesViewModel = {
  bufferMinutes: 15,
  rules: [
    {
      ruleId: 'rule-1',
      ruleType: 'RECURRING',
      ruleMode: 'AVAILABLE',
      daysOfWeek: [1, 2, 3, 4, 5],
      startTime: '09:00',
      endTime: '17:00',
      endDayOffset: 0,
      startDate: null,
      endDate: null,
    },
  ],
  inheritedRules: null,
};

describe('SchemaAvailabilityTabComponent', () => {
  let component: SchemaAvailabilityTabComponent;
  let fixture: ComponentFixture<SchemaAvailabilityTabComponent>;
  let availabilityServiceMock: {
    getSchemaRules: ReturnType<typeof vi.fn>;
    upsertSchemaRules: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    availabilityServiceMock = {
      getSchemaRules: vi.fn().mockReturnValue(of(mockRulesViewModel)),
      upsertSchemaRules: vi.fn().mockReturnValue(of(undefined)),
    };

    await TestBed.configureTestingModule({
      imports: [SchemaAvailabilityTabComponent, ReactiveFormsModule],
      providers: [
        {
          provide: AvailabilityService,
          useValue: availabilityServiceMock,
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(SchemaAvailabilityTabComponent);
    component = fixture.componentInstance;

    fixture.componentRef.setInput('schemaId', 'schema-uuid-123');
    fixture.componentRef.setInput('groupTimezone', 'Europe/Warsaw');

    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should call getSchemaRules on init with the provided schemaId', () => {
    expect(availabilityServiceMock.getSchemaRules).toHaveBeenCalledWith(
      'schema-uuid-123',
    );
  });

  it('should populate rules signal after successful load', () => {
    expect(component.rules().length).toBe(1);
    expect(component.rules()[0].ruleId).toBe('rule-1');
  });

  it('should populate bufferForm with loaded bufferMinutes', () => {
    expect(component.bufferForm.controls['bufferMinutes'].value).toBe(15);
  });

  it('should show error signal when load fails', () => {
    availabilityServiceMock.getSchemaRules.mockReturnValue(
      throwError(() => new Error('Network error')),
    );

    fixture.componentRef.setInput('schemaId', 'another-schema');
    fixture.detectChanges();

    expect(component.error()).toBe('Failed to load availability rules.');
    expect(component.isLoading()).toBe(false);
  });

  it('should set isLoading to false after load completes', () => {
    expect(component.isLoading()).toBe(false);
  });

  it('should add a rule when onAddRule is called with valid recurring form', () => {
    component.selectedRuleType.set('RECURRING');
    fixture.detectChanges();

    component.addRuleForm.patchValue({
      ruleMode: 'AVAILABLE',
      startTime: '10:00',
      endTime: '18:00',
    });
    // select some days
    component.onDayToggle(1);
    component.onDayToggle(2);

    const initialCount = component.rules().length;
    component.onAddRule();

    expect(component.rules().length).toBe(initialCount + 1);
    const added = component.rules()[component.rules().length - 1];
    expect(added.ruleType).toBe('RECURRING');
    expect(added.ruleMode).toBe('AVAILABLE');
    expect(added.startTime).toBe('10:00');
    expect(added.endTime).toBe('18:00');
    expect(added.daysOfWeek).toContain(1);
    expect(added.daysOfWeek).toContain(2);
  });

  it('should not add a rule when form is invalid', () => {
    component.selectedRuleType.set('RECURRING');
    fixture.detectChanges();

    // do not set required fields
    const initialCount = component.rules().length;
    component.onAddRule();

    expect(component.rules().length).toBe(initialCount);
  });

  it('should remove a rule when onRemoveRule is called', () => {
    expect(component.rules().length).toBe(1);
    component.onRemoveRule('rule-1');
    expect(component.rules().length).toBe(0);
  });

  it('should call upsertSchemaRules on save with correct payload', async () => {
    await component.onSave();

    expect(availabilityServiceMock.upsertSchemaRules).toHaveBeenCalledWith(
      'schema-uuid-123',
      expect.objectContaining({ bufferMinutes: 15 }),
    );
  });

  it('should set saveSuccess to true after successful save', async () => {
    await component.onSave();
    expect(component.saveSuccess()).toBe(true);
  });

  it('should set conflictError when save returns 400', async () => {
    availabilityServiceMock.upsertSchemaRules.mockReturnValue(
      throwError(() => ({
        type: 'conflict',
        detail: 'Overlapping AVAIL×AVAIL rules detected.',
      })),
    );

    await component.onSave();

    expect(component.conflictError()).toBe(
      'Overlapping AVAIL×AVAIL rules detected.',
    );
    expect(component.saveSuccess()).toBe(false);
  });

  it('should set error signal on generic save failure', async () => {
    availabilityServiceMock.upsertSchemaRules.mockReturnValue(
      throwError(() => new Error('Server error')),
    );

    await component.onSave();

    expect(component.error()).toBe(
      'Failed to save availability rules. Please try again.',
    );
  });

  it('should compute hasRules correctly', () => {
    expect(component.hasRules()).toBe(true);
    component.rules.set([]);
    expect(component.hasRules()).toBe(false);
  });

  it('should compute isRecurring based on selectedRuleType', () => {
    component.selectedRuleType.set('RECURRING');
    expect(component.isRecurring()).toBe(true);

    component.selectedRuleType.set('ONE_OFF');
    expect(component.isRecurring()).toBe(false);
  });

  it('should compute showNextDayBadge as false when no time values are set', () => {
    // showNextDayBadge is a computed signal that reads form control values.
    // It returns false when start/end are empty (initial state of a fresh RECURRING form).
    component.selectedRuleType.set('ONE_OFF');
    fixture.detectChanges();
    component.selectedRuleType.set('RECURRING');
    fixture.detectChanges();

    expect(component.showNextDayBadge()).toBe(false);
  });

  it('should format day numbers into short labels', () => {
    expect(component.formatDays([1, 2, 5])).toBe('Mon, Tue, Fri');
    expect(component.formatDays(null)).toBe('—');
    expect(component.formatDays([])).toBe('—');
  });

  it('should toggle day selection on onDayToggle', () => {
    component.selectedRuleType.set('RECURRING');
    fixture.detectChanges();

    expect(component.isDaySelected(1)).toBe(false);
    component.onDayToggle(1);
    expect(component.isDaySelected(1)).toBe(true);
    component.onDayToggle(1);
    expect(component.isDaySelected(1)).toBe(false);
  });

  it('should switch to ONE_OFF form when ruleType is changed', () => {
    component.onRuleTypeChange({
      target: { value: 'ONE_OFF' },
    } as unknown as Event);
    fixture.detectChanges();

    expect(component.selectedRuleType()).toBe('ONE_OFF');
    expect(component.addRuleForm.contains('startDate')).toBe(true);
    expect(component.addRuleForm.contains('daysOfWeek')).toBe(false);
  });

  it('should compute endDayOffset = 1 when end time is before start time', () => {
    component.selectedRuleType.set('RECURRING');
    fixture.detectChanges();

    component.onDayToggle(1);
    component.addRuleForm.patchValue({
      ruleMode: 'AVAILABLE',
      startTime: '22:00',
      endTime: '06:00',
    });

    component.onAddRule();

    const added = component.rules()[component.rules().length - 1];
    expect(added.endDayOffset).toBe(1);
  });
});
