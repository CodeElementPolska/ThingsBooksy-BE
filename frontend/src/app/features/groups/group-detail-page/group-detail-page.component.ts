import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { GroupContextStore, SchemaSummary } from '../group-context.store';
import { GroupsService } from '../groups.service';
import { AnimatedBackgroundComponent } from '../../../shared/components/animated-background/animated-background.component';
import { CreateOrEditGroupModalComponent } from '../../dashboard/create-group-modal/create-group-modal.component';
import { CreateResourceModalComponent } from '../create-resource-modal/create-resource-modal.component';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { NotificationService } from '../../../shared/services/notification.service';
import { GroupHeaderPanelComponent } from '../group-header-panel/group-header-panel.component';
import { SchemasPanelComponent } from '../schemas-panel/schemas-panel.component';
import { ResourcesPanelComponent } from '../resources-panel/resources-panel.component';
import { MembersPanelComponent } from '../members-panel/members-panel.component';
import { ResourcesApiService } from '../services/resources-api.service';

@Component({
  selector: 'tb-group-detail-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [GroupContextStore],
  imports: [
    RouterModule,
    AnimatedBackgroundComponent,
    CreateOrEditGroupModalComponent,
    CreateResourceModalComponent,
    GroupHeaderPanelComponent,
    SchemasPanelComponent,
    ResourcesPanelComponent,
    MembersPanelComponent,
  ],
  templateUrl: './group-detail-page.component.html',
  styleUrl: './group-detail-page.component.scss',
})
export class GroupDetailPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly store = inject(GroupContextStore);
  private readonly groupsService = inject(GroupsService);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly notifications = inject(NotificationService);

  readonly editOpen = signal(false);
  readonly deleteLoading = signal(false);
  readonly resourceModalOpen = signal(false);
  readonly resourceModalSchemaId = signal<string | null>(null);

  private readonly resourcesApi = inject(ResourcesApiService);

  private readonly paramMap = toSignal(this.route.paramMap);
  readonly groupId = computed(() => this.paramMap()?.get('groupId') ?? null);

  readonly groupName = computed(() => this.store.group()?.name ?? '');
  readonly initialLoading = this.store.initialLoading;
  readonly initialError = this.store.initialError;
  readonly group = this.store.group;
  readonly schemas = this.store.schemasWithCounts;
  readonly resources = this.store.resources;
  readonly members = this.store.members;

  constructor() {
    effect(() => {
      const id = this.groupId();
      if (!id) {
        void this.router.navigate(['/dashboard']);
        return;
      }
      void this.store.loadGroup(id);
    });
  }

  ngOnInit(): void {
    // Intentionally empty — data loading is driven by the effect() in constructor.
  }

  onEdit(): void {
    this.editOpen.set(true);
  }

  onEditModalClose(): void {
    this.editOpen.set(false);
  }

  onEditSubmitted(): void {
    const id = this.groupId();
    if (id) {
      void this.store.loadGroup(id);
    }
    this.notifications.success('Group updated');
    this.editOpen.set(false);
  }

  async onDelete(): Promise<void> {
    const id = this.groupId();
    if (!id) return;

    const schemaCount = this.schemas().length;
    const resourceCount = this.resources().items.length;

    const confirmed = await this.confirmDialog.confirm({
      title: 'Delete group',
      message: `Are you sure you want to delete this group? It contains ${schemaCount} schema(s) and ${resourceCount} resource(s). This action cannot be undone.`,
      confirmLabel: 'Delete',
      cancelLabel: 'Cancel',
      danger: true,
    });

    if (!confirmed) return;

    this.deleteLoading.set(true);
    try {
      await firstValueFrom(this.groupsService.deleteGroup(id));
      this.notifications.success('Group deleted');
      void this.router.navigate(['/dashboard']);
    } catch {
      this.notifications.error('Failed to delete group. Please try again.');
    } finally {
      this.deleteLoading.set(false);
    }
  }

  onAddSchema(): void {
    const id = this.groupId();
    if (id) {
      void this.router.navigate(['/groups', id, 'schemas', 'new']);
    }
  }

  onSelectSchema(schemaId: string): void {
    const id = this.groupId();
    if (id) {
      void this.router.navigate(['/groups', id, 'schemas', schemaId]);
    }
  }

  onAddResource(): void {
    this.resourceModalSchemaId.set(null);
    this.resourceModalOpen.set(true);
  }

  onAddResourceForSchema(schemaId: string): void {
    this.resourceModalSchemaId.set(schemaId);
    this.resourceModalOpen.set(true);
  }

  onResourceModalClose(): void {
    this.resourceModalOpen.set(false);
  }

  onResourceClick(resourceId: string): void {
    const id = this.groupId();
    if (id) {
      void this.router.navigate(['/groups', id, 'resources', resourceId]);
    }
  }

  async onResourceCreated(payload: { id: string; resourceTypeId: string; name: string }): Promise<void> {
    this.resourceModalOpen.set(false);
    const id = this.groupId();
    if (!id) return;
    // Refresh first page so the new resource (and any concurrent inserts) is reflected.
    void this.store.loadGroup(id);
    // Suppress unused parameter warning; payload available if we want optimistic prepend later.
    void payload;
  }

  async onDeleteSchema(schema: SchemaSummary): Promise<void> {
    const instanceCount = this.store.countResourcesOfSchema(schema.id);
    const confirmed = await this.confirmDialog.confirm({
      title: `Delete schema '${schema.name}'?`,
      message:
        instanceCount > 0
          ? `${instanceCount} loaded resource(s) of this type will also be deleted.`
          : 'This schema will be deleted.',
      confirmLabel: 'Delete',
      cancelLabel: 'Cancel',
      danger: true,
    });
    if (!confirmed) return;

    try {
      await firstValueFrom(this.resourcesApi.deleteResourceType(schema.id));
      this.store.removeSchema(schema.id);
      this.notifications.success('Schema deleted');
    } catch {
      // errorInterceptor surfaces toast
    }
  }

  onRetry(): void {
    const id = this.groupId();
    if (id) {
      void this.store.loadGroup(id);
    }
  }
}
