import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  ThingsBooksyModulesManagementGroupsCoreFeaturesGetGroupMembersGroupMemberDto as MemberDto,
  ThingsBooksyModulesResourcesCoreFeaturesGetResourceInstancesResourceInstanceRowDto as ResourceRowDto,
} from '../../api/data-contracts';
import { AuthService } from '../auth/auth.service';
import { GroupDetailDto, GroupsApiService } from './services/groups-api.service';
import {
  PropertyDefinitionDto,
  ResourcesApiService,
  ResourceSchemaSummaryDto,
} from './services/resources-api.service';

export interface SchemaSummary {
  readonly id: string;
  readonly name: string;
  readonly description: string | null;
  readonly propertyDefinitionsCount: number;
  readonly propertyDefinitions: PropertyDefinitionDto[];
  readonly resourceCount: number;
}

export interface CursorPage<T> {
  readonly items: T[];
  readonly nextCursor: string | null;
  readonly loading: boolean;
}

const PAGE_SIZE = 20;

@Injectable()
export class GroupContextStore {
  private readonly groupsApi = inject(GroupsApiService);
  private readonly resourcesApi = inject(ResourcesApiService);
  private readonly auth = inject(AuthService);

  private readonly _group = signal<GroupDetailDto | null>(null);
  private readonly _schemas = signal<SchemaSummary[]>([]);
  private readonly _members = signal<CursorPage<MemberDto>>({
    items: [],
    nextCursor: null,
    loading: false,
  });
  private readonly _resources = signal<CursorPage<ResourceRowDto>>({
    items: [],
    nextCursor: null,
    loading: false,
  });
  private readonly _initialLoading = signal(false);
  private readonly _initialError = signal<string | null>(null);

  readonly group = this._group.asReadonly();
  readonly schemas = this._schemas.asReadonly();
  readonly schemasWithCounts = computed(() => {
    const resourceItems = this._resources().items;
    return this._schemas().map(s => ({
      ...s,
      resourceCount: resourceItems.filter(r => r.resourceSchemaId === s.id).length,
    }));
  });
  readonly members = this._members.asReadonly();
  readonly resources = this._resources.asReadonly();
  readonly initialLoading = this._initialLoading.asReadonly();
  readonly initialError = this._initialError.asReadonly();

  readonly isOwner = computed(() => {
    const me = this.auth.currentUser();
    const owner = this._group()?.ownerId;
    return !!me && !!owner && me.id === owner;
  });

  async loadGroup(groupId: string): Promise<void> {
    this._initialLoading.set(true);
    this._initialError.set(null);
    try {
      const [group, schemas, membersPage, resourcesPage] = await Promise.all([
        firstValueFrom(this.groupsApi.getGroup(groupId)),
        firstValueFrom(this.resourcesApi.getResourceSchemas(groupId)),
        firstValueFrom(this.groupsApi.getGroupMembers(groupId, { take: PAGE_SIZE })),
        firstValueFrom(
          this.resourcesApi.getResourceInstances({ groupId, take: PAGE_SIZE }),
        ),
      ]);
      this._group.set(group);
      this._schemas.set(this.mapSchemas(schemas));
      this._members.set({
        items: membersPage.items ?? [],
        nextCursor: membersPage.nextCursor ?? null,
        loading: false,
      });
      this._resources.set({
        items: resourcesPage.items ?? [],
        nextCursor: resourcesPage.nextCursor ?? null,
        loading: false,
      });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to load group.';
      this._initialError.set(message);
      this._group.set(null);
    } finally {
      this._initialLoading.set(false);
    }
  }

  async loadMoreMembers(groupId: string): Promise<void> {
    const current = this._members();
    if (current.loading || !current.nextCursor) {
      return;
    }
    this._members.set({ ...current, loading: true });
    try {
      const page = await firstValueFrom(
        this.groupsApi.getGroupMembers(groupId, {
          afterId: current.nextCursor,
          take: PAGE_SIZE,
        }),
      );
      this._members.set({
        items: [...current.items, ...(page.items ?? [])],
        nextCursor: page.nextCursor ?? null,
        loading: false,
      });
    } catch {
      this._members.set({ ...current, loading: false });
    }
  }

  async loadMoreResources(groupId: string): Promise<void> {
    const current = this._resources();
    if (current.loading || !current.nextCursor) {
      return;
    }
    this._resources.set({ ...current, loading: true });
    try {
      const page = await firstValueFrom(
        this.resourcesApi.getResourceInstances({
          groupId,
          afterId: current.nextCursor,
          take: PAGE_SIZE,
        }),
      );
      this._resources.set({
        items: [...current.items, ...(page.items ?? [])],
        nextCursor: page.nextCursor ?? null,
        loading: false,
      });
    } catch {
      this._resources.set({ ...current, loading: false });
    }
  }

  replaceGroup(group: GroupDetailDto): void {
    this._group.set(group);
  }

  removeGroup(): void {
    this._group.set(null);
  }

  prependResource(resource: ResourceRowDto): void {
    const current = this._resources();
    this._resources.set({ ...current, items: [resource, ...current.items] });
  }

  addSchema(schema: ResourceSchemaSummaryDto): void {
    this._schemas.set([...this._schemas(), this.toSummary(schema)]);
  }

  replaceSchema(schema: ResourceSchemaSummaryDto): void {
    this._schemas.set(
      this._schemas().map(s => (s.id === schema.id ? this.toSummary(schema) : s)),
    );
  }

  removeSchema(schemaId: string): void {
    this._schemas.set(this._schemas().filter(s => s.id !== schemaId));
    const r = this._resources();
    this._resources.set({
      ...r,
      items: r.items.filter(row => row.resourceSchemaId !== schemaId),
    });
  }

  countResourcesOfSchema(schemaId: string): number {
    return this._resources().items.filter(r => r.resourceSchemaId === schemaId).length;
  }

  private mapSchemas(raw: ResourceSchemaSummaryDto[]): SchemaSummary[] {
    return raw.map(r => this.toSummary(r));
  }

  private toSummary(schema: ResourceSchemaSummaryDto): SchemaSummary {
    return {
      id: schema.id,
      name: schema.name,
      description: schema.description ?? null,
      propertyDefinitionsCount: schema.propertyDefinitions?.length ?? 0,
      propertyDefinitions: schema.propertyDefinitions ?? [],
      resourceCount: 0,
    };
  }
}
