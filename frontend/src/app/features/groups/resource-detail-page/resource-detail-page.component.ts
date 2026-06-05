import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { GroupsApiService } from '../services/groups-api.service';
import { ResourcesApiService } from '../services/resources-api.service';
import { ResourceAvailabilityTabComponent } from '../../availability/components/resource-availability-tab/resource-availability-tab.component';
import { NotificationService } from '../../../shared/services/notification.service';

@Component({
  selector: 'tb-resource-detail-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, ResourceAvailabilityTabComponent],
  templateUrl: './resource-detail-page.component.html',
  styleUrl: './resource-detail-page.component.scss',
})
export class ResourceDetailPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly groupsApi = inject(GroupsApiService);
  private readonly resourcesApi = inject(ResourcesApiService);
  private readonly notifications = inject(NotificationService);

  readonly groupId = signal<string>('');
  readonly resourceId = signal<string>('');
  readonly resourceName = signal<string>('');
  readonly schemaName = signal<string>('');
  readonly groupTimezone = signal<string>('UTC');
  readonly isLoading = signal(true);

  readonly title = computed(() => this.resourceName() || 'Resource');

  async ngOnInit(): Promise<void> {
    const groupId = this.route.snapshot.paramMap.get('groupId') ?? '';
    const resourceId = this.route.snapshot.paramMap.get('resourceId') ?? '';
    this.groupId.set(groupId);
    this.resourceId.set(resourceId);

    try {
      const [resource, group] = await Promise.all([
        firstValueFrom(this.resourcesApi.getResourceInstance(resourceId)),
        firstValueFrom(this.groupsApi.getGroup(groupId)),
      ]);
      this.resourceName.set(resource.name ?? 'Resource');
      this.schemaName.set(resource.resourceTypeName ?? '');
      const groupAny = group as Record<string, unknown>;
      this.groupTimezone.set((groupAny['timeZoneId'] as string | undefined) ?? 'UTC');
    } catch {
      this.notifications.error('Failed to load resource.');
      void this.router.navigate(['/groups', groupId]);
    } finally {
      this.isLoading.set(false);
    }
  }
}
