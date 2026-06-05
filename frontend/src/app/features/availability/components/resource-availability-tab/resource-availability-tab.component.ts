import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { AvailabilityService } from '../../availability.service';
import {
  AvailabilityRule,
  AvailabilityRuleMode,
  AvailabilityRuleType,
  AvailabilityRulesViewModel,
  isAvailabilityConflictError,
} from '../../models/availability.model';
import { ConfirmDialogService } from '../../../../shared/services/confirm-dialog.service';
import { NotificationService } from '../../../../shared/services/notification.service';

const DAY_LABELS: { value: number; label: string }[] = [
  { value: 1, label: 'Mon' },
  { value: 2, label: 'Tue' },
  { value: 3, label: 'Wed' },
  { value: 4, label: 'Thu' },
  { value: 5, label: 'Fri' },
  { value: 6, label: 'Sat' },
  { value: 0, label: 'Sun' },
];

@Component({
  selector: 'tb-resource-availability-tab',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule],
  templateUrl: './resource-availability-tab.component.html',
  styleUrl: './resource-availability-tab.component.scss',
})
export class ResourceAvailabilityTabComponent implements OnInit {
  // Dependencies
  private readonly availabilityService = inject(AvailabilityService);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly notifications = inject(NotificationService);

  // Inputs
  readonly resourceId = input.required<string>();
  readonly groupTimezone = input<string>('UTC');

  // State signals
  readonly isLoading = signal(false);
  readonly isSaving = signal(false);
  readonly isResetting = signal(false);
  readonly error = signal<string | null>(null);
  readonly conflictError = signal<string | null>(null);
  readonly saveSuccess = signal(false);

  // Data signals
  readonly rulesViewModel = signal<AvailabilityRulesViewModel | null>(null);
  readonly rules = signal<AvailabilityRule[]>([]);
  readonly inheritedRules = signal<AvailabilityRule[] | null>(null);
  readonly selectedRuleType = signal<AvailabilityRuleType>('RECURRING');

  // Computed
  readonly hasRules = computed(() => this.rules().length > 0);
  readonly hasInheritedRules = computed(
    () => (this.inheritedRules()?.length ?? 0) > 0,
  );
  readonly isRecurring = computed(
    () => this.selectedRuleType() === 'RECURRING',
  );
  readonly showNextDayBadge = computed(() => {
    const start = this.addRuleForm?.controls['startTime']?.value as string;
    const end = this.addRuleForm?.controls['endTime']?.value as string;
    if (!start || !end) return false;
    return end < start;
  });
  readonly effectiveBufferMinutes = computed(
    () => this.rulesViewModel()?.bufferMinutes ?? 0,
  );

  // Exposed day labels for template iteration
  readonly dayLabels = DAY_LABELS;

  // Buffer override form
  readonly bufferForm = this.fb.group({
    bufferMinutes: this.fb.control<number | null>(null, {
      validators: [Validators.min(0), Validators.max(1440)],
    }),
  });

  // Add rule form (typed loosely as FormGroup — rebuilt on type switch)
  addRuleForm!: FormGroup;

  constructor() {
    // Rebuild form when rule type changes
    effect(() => {
      const ruleType = this.selectedRuleType();
      untracked(() => this.buildAddRuleForm(ruleType));
    });

    // Reload data when resourceId changes
    effect(() => {
      const id = this.resourceId();
      if (id) {
        untracked(() => this.loadRules(id));
      }
    });
  }

  ngOnInit(): void {
    this.buildAddRuleForm(this.selectedRuleType());
  }

  onRuleTypeChange(event: Event): void {
    const value = (event.target as HTMLSelectElement)
      .value as AvailabilityRuleType;
    this.selectedRuleType.set(value);
  }

  isDaySelected(day: number): boolean {
    const control = this.addRuleForm?.get('daysOfWeek');
    if (!control) return false;
    const days = control.value as number[];
    return days.includes(day);
  }

  onDayToggle(day: number): void {
    const control = this.addRuleForm?.get('daysOfWeek');
    if (!control) return;
    const current = (control.value as number[]) ?? [];
    const updated = current.includes(day)
      ? current.filter(d => d !== day)
      : [...current, day];
    control.setValue(updated);
    control.markAsTouched();
  }

  onAddRule(): void {
    if (this.addRuleForm.invalid) {
      this.addRuleForm.markAllAsTouched();
      return;
    }

    const raw = this.addRuleForm.getRawValue();
    const isRecurring = this.selectedRuleType() === 'RECURRING';

    const newRule: AvailabilityRule = {
      ruleId: crypto.randomUUID(),
      ruleType: this.selectedRuleType(),
      ruleMode: raw['ruleMode'] as AvailabilityRuleMode,
      daysOfWeek: isRecurring ? (raw['daysOfWeek'] as number[]) : null,
      startTime: raw['startTime'] as string,
      endTime: raw['endTime'] as string,
      endDayOffset:
        (raw['endTime'] as string) < (raw['startTime'] as string) ? 1 : 0,
      startDate: !isRecurring ? (raw['startDate'] as string) : null,
      endDate: !isRecurring ? (raw['endDate'] as string) : null,
    };

    this.rules.update(current => [...current, newRule]);
    this.buildAddRuleForm(this.selectedRuleType());
    this.conflictError.set(null);
  }

  onRemoveRule(ruleId: string): void {
    this.rules.update(current => current.filter(r => r.ruleId !== ruleId));
  }

  async onSave(): Promise<void> {
    if (this.bufferForm.invalid || this.isSaving()) return;

    const bufferOverride = this.bufferForm.getRawValue().bufferMinutes;
    this.isSaving.set(true);
    this.error.set(null);
    this.conflictError.set(null);
    this.saveSuccess.set(false);

    try {
      await firstValueFrom(
        this.availabilityService.upsertResourceRules(this.resourceId(), {
          bufferMinutes: bufferOverride ?? 0,
          rules: this.rules().map(r => ({
            ruleType: r.ruleType,
            ruleMode: r.ruleMode,
            daysOfWeek: r.daysOfWeek,
            startTime: r.startTime,
            endTime: r.endTime,
            startDate: r.startDate,
            endDate: r.endDate,
          })),
        }),
      );
      this.saveSuccess.set(true);
      this.notifications.success('Resource availability saved.');
    } catch (err) {
      if (isAvailabilityConflictError(err)) {
        this.conflictError.set(err.detail);
      } else {
        this.error.set('Failed to save availability rules. Please try again.');
      }
    } finally {
      this.isSaving.set(false);
    }
  }

  async onResetToDefaults(): Promise<void> {
    const confirmed = await this.confirmDialog.confirm({
      title: 'Reset to schema defaults',
      message:
        'This will remove all resource-level overrides and revert to the schema rules. This action cannot be undone.',
      confirmLabel: 'Reset',
      cancelLabel: 'Cancel',
      danger: true,
    });

    if (!confirmed) return;

    this.isResetting.set(true);
    this.error.set(null);
    this.conflictError.set(null);

    try {
      await firstValueFrom(
        this.availabilityService.upsertResourceRules(this.resourceId(), {
          bufferMinutes: 0,
          rules: [],
        }),
      );
      // Reload to reflect inherited state
      this.loadRules(this.resourceId());
      this.notifications.success('Reset to schema defaults.');
    } catch {
      this.error.set('Failed to reset to schema defaults. Please try again.');
    } finally {
      this.isResetting.set(false);
    }
  }

  formatDays(daysOfWeek: number[] | null): string {
    if (!daysOfWeek || daysOfWeek.length === 0) return '—';
    return daysOfWeek
      .map(d => DAY_LABELS.find(dl => dl.value === d)?.label ?? String(d))
      .join(', ');
  }

  private loadRules(resourceId: string): void {
    this.isLoading.set(true);
    this.error.set(null);
    this.saveSuccess.set(false);

    this.availabilityService
      .getResourceRules(resourceId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (vm: AvailabilityRulesViewModel) => {
          this.rulesViewModel.set(vm);
          this.rules.set(vm.rules);
          this.inheritedRules.set(vm.inheritedRules);
          this.bufferForm.patchValue({ bufferMinutes: null });
          this.isLoading.set(false);
        },
        error: () => {
          this.error.set('Failed to load availability rules.');
          this.isLoading.set(false);
        },
      });
  }

  private buildAddRuleForm(ruleType: AvailabilityRuleType): void {
    const today = new Date().toISOString().split('T')[0];
    const isRecurring = ruleType === 'RECURRING';

    if (isRecurring) {
      this.addRuleForm = this.fb.group({
        ruleMode: this.fb.control<AvailabilityRuleMode>('AVAILABLE', {
          nonNullable: true,
          validators: [Validators.required],
        }),
        daysOfWeek: this.fb.control<number[]>([], {
          nonNullable: true,
          validators: [Validators.required],
        }),
        startTime: this.fb.control('', {
          nonNullable: true,
          validators: [
            Validators.required,
            Validators.pattern(/^\d{2}:\d{2}$/),
          ],
        }),
        endTime: this.fb.control('', {
          nonNullable: true,
          validators: [
            Validators.required,
            Validators.pattern(/^\d{2}:\d{2}$/),
          ],
        }),
      });
    } else {
      this.addRuleForm = this.fb.group({
        ruleMode: this.fb.control<AvailabilityRuleMode>('AVAILABLE', {
          nonNullable: true,
          validators: [Validators.required],
        }),
        startDate: this.fb.control(today, {
          nonNullable: true,
          validators: [Validators.required],
        }),
        endDate: this.fb.control(today, {
          nonNullable: true,
          validators: [Validators.required],
        }),
        startTime: this.fb.control('', {
          nonNullable: true,
          validators: [
            Validators.required,
            Validators.pattern(/^\d{2}:\d{2}$/),
          ],
        }),
        endTime: this.fb.control('', {
          nonNullable: true,
          validators: [
            Validators.required,
            Validators.pattern(/^\d{2}:\d{2}$/),
          ],
        }),
      });
    }
  }
}
