import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import {
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ModalComponent } from '../../../shared/components/modal/modal.component';
import { NotificationService } from '../../../shared/services/notification.service';
import { GroupContextStore, SchemaSummary } from '../group-context.store';
import {
  PropertyDefinitionDto,
  ResourcesApiService,
} from '../services/resources-api.service';

@Component({
  selector: 'tb-create-resource-modal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ModalComponent, ReactiveFormsModule],
  templateUrl: './create-resource-modal.component.html',
  styleUrl: './create-resource-modal.component.scss',
})
export class CreateResourceModalComponent {
  readonly open = input.required<boolean>();
  readonly groupId = input.required<string>();
  readonly preselectedSchemaId = input<string | null>(null);

  readonly close = output<void>();
  readonly created = output<{ id: string; resourceTypeId: string; name: string }>();

  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ResourcesApiService);
  private readonly store = inject(GroupContextStore);
  private readonly notifications = inject(NotificationService);

  readonly selectedSchemaId = signal<string | null>(null);
  readonly submitting = signal(false);

  readonly schemas = this.store.schemas;

  readonly selectedSchema = computed<SchemaSummary | null>(() => {
    const id = this.selectedSchemaId();
    if (!id) return null;
    return this.schemas().find(s => s.id === id) ?? null;
  });

  readonly properties = computed<PropertyDefinitionDto[]>(
    () => this.selectedSchema()?.propertyDefinitions ?? [],
  );

  readonly form = this.fb.group({
    name: this.fb.control('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
    description: this.fb.control<string | null>(null, {
      validators: [Validators.maxLength(500)],
    }),
    properties: this.fb.group({}),
  });

  constructor() {
    effect(() => {
      if (this.open()) {
        const preset = this.preselectedSchemaId();
        this.selectedSchemaId.set(preset ?? null);
        this.rebuildPropertyControls(untracked(() => this.properties()));
        this.form.controls.name.reset('');
        this.form.controls.description.reset(null);
      }
    });

    effect(() => {
      const props = this.properties();
      if (this.open()) {
        this.rebuildPropertyControls(props);
      }
    });
  }

  onSchemaPick(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.selectedSchemaId.set(value || null);
  }

  onCancel(): void {
    this.close.emit();
  }

  async onSubmit(): Promise<void> {
    if (!this.selectedSchemaId() || this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const propsControl = this.form.controls.properties as FormGroup;
    const propertyValues = this.properties().map(p => ({
      propertyDefinitionId: p.id,
      value: (propsControl.get(p.id)?.value ?? '').toString(),
    }));

    this.submitting.set(true);
    try {
      const result = await firstValueFrom(
        this.api.createResourceInstance({
          resourceTypeId: this.selectedSchemaId()!,
          name: raw.name.trim(),
          description: raw.description ?? null,
          propertyValues,
        }),
      );
      this.notifications.success('Resource created');
      this.created.emit({
        id: result.id,
        resourceTypeId: this.selectedSchemaId()!,
        name: raw.name.trim(),
      });
    } catch {
      // errorInterceptor surfaces toast
    } finally {
      this.submitting.set(false);
    }
  }

  private rebuildPropertyControls(properties: PropertyDefinitionDto[]): void {
    const group = this.form.controls.properties as FormGroup;
    Object.keys(group.controls).forEach(key => group.removeControl(key));
    for (const prop of properties) {
      const validators = [];
      if (prop.isRequired) validators.push(Validators.required);
      if (prop.dataType === 'Number') {
        validators.push(Validators.pattern(/^-?\d+(\.\d+)?$/));
      }
      const initial = prop.dataType === 'Boolean' ? 'false' : '';
      group.addControl(
        prop.id,
        this.fb.control<string | null>(initial, { validators }),
      );
    }
  }
}
