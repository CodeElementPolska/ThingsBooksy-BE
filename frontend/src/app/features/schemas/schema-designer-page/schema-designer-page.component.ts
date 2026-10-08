import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  HostListener,
  OnDestroy,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { NotificationService } from '../../../shared/services/notification.service';
import { ResourcesApiService } from '../../groups/services/resources-api.service';
import { SchemaFormPanelComponent } from '../schema-form-panel/schema-form-panel.component';
import { SchemaPreviewPanelComponent } from '../schema-preview-panel/schema-preview-panel.component';
import {
  FieldDataType,
  FieldDraft,
  createEmptyField,
  dataTypeToEnum,
  parseServerDataType,
} from '../types';

interface InitialSnapshot {
  name: string;
  description: string | null;
  fields: FieldDraft[];
}

function snapshotMatches(current: InitialSnapshot, initial: InitialSnapshot): boolean {
  if (current.name.trim() !== initial.name.trim()) return false;
  if ((current.description ?? '') !== (initial.description ?? '')) return false;
  if (current.fields.length !== initial.fields.length) return false;
  for (let i = 0; i < current.fields.length; i++) {
    const a = current.fields[i];
    const b = initial.fields[i];
    if (a.name !== b.name) return false;
    if (a.dataType !== b.dataType) return false;
    if (a.isRequired !== b.isRequired) return false;
    if (a.serverId !== b.serverId) return false;
  }
  return true;
}

@Component({
  selector: 'tb-schema-designer-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, SchemaFormPanelComponent, SchemaPreviewPanelComponent],
  templateUrl: './schema-designer-page.component.html',
  styleUrl: './schema-designer-page.component.scss',
})
export class SchemaDesignerPageComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(ResourcesApiService);
  private readonly notifications = inject(NotificationService);
  private readonly destroyRef = inject(DestroyRef);

  readonly groupId = signal<string>('');
  readonly schemaId = signal<string | null>(null);
  readonly name = signal<string>('');
  readonly description = signal<string | null>(null);
  readonly fields = signal<FieldDraft[]>([]);
  readonly isLoading = signal(false);
  readonly isSaving = signal(false);
  readonly justSaved = signal(false);
  readonly nameError = signal<string | null>(null);

  private initial: InitialSnapshot = { name: '', description: null, fields: [] };

  readonly mode = computed<'create' | 'edit'>(() => (this.schemaId() ? 'edit' : 'create'));

  readonly dirty = computed(() => {
    const current: InitialSnapshot = {
      name: this.name(),
      description: this.description(),
      fields: this.fields(),
    };
    return !snapshotMatches(current, this.initial);
  });

  readonly title = computed(() =>
    this.mode() === 'create' ? 'New schema' : 'Edit schema',
  );

  readonly badgeState = computed<'saved' | 'unsaved' | 'idle'>(() => {
    if (this.justSaved()) return 'saved';
    if (this.dirty()) return 'unsaved';
    return 'idle';
  });

  readonly badgeLabel = computed(() => {
    switch (this.badgeState()) {
      case 'saved':
        return 'Saved ✓';
      case 'unsaved':
        return 'Unsaved changes';
      default:
        return 'No changes';
    }
  });

  readonly canSubmit = computed(() => {
    if (this.isSaving()) return false;
    if (!this.name().trim()) return false;
    if (this.nameError()) return false;
    return this.dirty();
  });

  async ngOnInit(): Promise<void> {
    const groupId = this.route.snapshot.paramMap.get('groupId')
      ?? this.route.parent?.snapshot.paramMap.get('groupId')
      ?? this.route.pathFromRoot
        .map(r => r.snapshot.paramMap.get('groupId'))
        .find(v => !!v)
      ?? '';
    this.groupId.set(groupId);

    const schemaId = this.route.snapshot.paramMap.get('schemaId');
    this.schemaId.set(schemaId);

    if (schemaId) {
      await this.loadSchema(schemaId);
    } else {
      this.initial = { name: '', description: null, fields: [] };
    }
  }

  ngOnDestroy(): void {
    // HostListener is auto-cleaned by Angular.
  }

  @HostListener('window:beforeunload', ['$event'])
  onBeforeUnload(event: BeforeUnloadEvent): void {
    if (this.dirty()) {
      event.preventDefault();
      event.returnValue = '';
    }
  }

  onNameChange(value: string): void {
    this.name.set(value);
    this.justSaved.set(false);
    this.validateName(value);
  }

  onDescriptionChange(value: string | null): void {
    this.description.set(value);
    this.justSaved.set(false);
  }

  onFieldsChange(value: FieldDraft[]): void {
    this.fields.set(value);
    this.justSaved.set(false);
  }

  async onSave(): Promise<void> {
    if (!this.canSubmit()) return;

    if (this.fields().some(f => !f.name.trim())) {
      this.notifications.error('Every field must have a name.');
      return;
    }

    this.isSaving.set(true);
    try {
      const propertyDefinitions = this.fields().map(f => ({
        id: f.serverId ?? undefined,
        name: f.name.trim(),
        dataType: dataTypeToEnum(f.dataType),
        isRequired: f.isRequired,
      }));

      if (this.mode() === 'create') {
        await firstValueFrom(
          this.api.createResourceSchema({
            groupId: this.groupId(),
            name: this.name().trim(),
            description: this.description(),
            propertyDefinitions,
          }),
        );
      } else {
        await firstValueFrom(
          this.api.updateResourceSchema(this.schemaId()!, {
            name: this.name().trim(),
            description: this.description(),
            propertyDefinitions,
          }),
        );
      }

      this.initial = {
        name: this.name(),
        description: this.description(),
        fields: this.fields(),
      };
      this.justSaved.set(true);
      this.notifications.success('Schema saved');

      setTimeout(() => {
        this.router.navigate(['/groups', this.groupId()]);
      }, 600);
    } catch {
      // errorInterceptor surfaces toast
    } finally {
      this.isSaving.set(false);
    }
  }

  onCancel(): void {
    this.router.navigate(['/groups', this.groupId()]);
  }

  private async loadSchema(schemaId: string): Promise<void> {
    this.isLoading.set(true);
    try {
      const schema = await firstValueFrom(this.api.getResourceSchema(schemaId));
      const fields: FieldDraft[] = (schema.propertyDefinitions ?? []).map(p => ({
        id: crypto.randomUUID(),
        serverId: p.id,
        name: p.name,
        dataType: parseServerDataType(p.dataType as unknown as string),
        isRequired: p.isRequired,
      }));
      this.name.set(schema.name);
      this.description.set(schema.description ?? null);
      this.fields.set(fields);
      this.initial = {
        name: schema.name,
        description: schema.description ?? null,
        fields: fields.map(f => ({ ...f })),
      };
    } catch {
      this.notifications.error('Failed to load schema.');
      this.router.navigate(['/groups', this.groupId()]);
    } finally {
      this.isLoading.set(false);
    }
  }

  private validateName(value: string): void {
    const trimmed = value.trim();
    if (!trimmed) {
      this.nameError.set('Schema name is required.');
      return;
    }
    if (trimmed.length > 100) {
      this.nameError.set('Schema name must be 100 characters or fewer.');
      return;
    }
    this.nameError.set(null);
  }
}
