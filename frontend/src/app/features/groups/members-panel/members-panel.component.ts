import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
} from '@angular/core';
import { ThingsBooksyModulesManagementGroupsCoreFeaturesGetGroupMembersGroupMemberDto } from '../../../api/data-contracts';
import { AvatarComponent } from '../../../shared/components/avatar/avatar.component';
import { CountChipComponent } from '../../../shared/components/count-chip/count-chip.component';
import { IconPlusComponent } from '../../../shared/components/icon-plus/icon-plus.component';
import { InfiniteScrollDirective } from '../../../shared/directives/infinite-scroll.directive';

function getInitials(email: string | null | undefined): string {
  if (!email) {
    return '?';
  }
  const local = email.split('@')[0] ?? '';
  return local.slice(0, 2).toUpperCase() || '?';
}

@Component({
  selector: 'tb-members-panel',
  standalone: true,
  imports: [AvatarComponent, CountChipComponent, IconPlusComponent, InfiniteScrollDirective],
  templateUrl: './members-panel.component.html',
  styleUrl: './members-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MembersPanelComponent {
  readonly members = input.required<readonly ThingsBooksyModulesManagementGroupsCoreFeaturesGetGroupMembersGroupMemberDto[]>();
  readonly nextCursor = input<string | null>(null);
  readonly loadingMore = input<boolean>(false);

  readonly loadMore = output<void>();

  readonly memberCount = computed(() => this.members().length);

  readonly infiniteScrollDisabled = computed(
    () => this.loadingMore() || this.nextCursor() === null,
  );

  getInitials(email: string | null | undefined): string {
    return getInitials(email);
  }
}
