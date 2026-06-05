import {
  ChangeDetectionStrategy,
  Component,
  InputSignal,
  OutputEmitterRef,
  input,
  output,
} from '@angular/core';
import { ThingsBooksyModulesResourcesCoreFeaturesGetResourceInstancesResourceInstanceRowDto as ResourceRowDto } from '../../../api/data-contracts';
import { SchemaSummary } from '../group-context.store';
import { CountChipComponent } from '../../../shared/components/count-chip/count-chip.component';
import { InfiniteScrollDirective } from '../../../shared/directives/infinite-scroll.directive';
import { IconChevronComponent } from '../../../shared/components/icon-chevron/icon-chevron.component';
import { IconPlusComponent } from '../../../shared/components/icon-plus/icon-plus.component';

@Component({
  selector: 'tb-resources-panel',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CountChipComponent,
    InfiniteScrollDirective,
    IconChevronComponent,
    IconPlusComponent,
  ],
  templateUrl: './resources-panel.component.html',
  styleUrl: './resources-panel.component.scss',
})
export class ResourcesPanelComponent {
  readonly resources: InputSignal<readonly ResourceRowDto[]> = input<readonly ResourceRowDto[]>([]);
  readonly schemas: InputSignal<readonly SchemaSummary[]> = input<readonly SchemaSummary[]>([]);
  readonly nextCursor: InputSignal<string | null> = input<string | null>(null);
  readonly loadingMore: InputSignal<boolean> = input<boolean>(false);
  readonly isOwner: InputSignal<boolean> = input<boolean>(false);

  readonly loadMore: OutputEmitterRef<void> = output<void>();
  readonly addResource: OutputEmitterRef<void> = output<void>();
  readonly resourceClick: OutputEmitterRef<string> = output<string>();

  getSchemaName(resourceTypeId: string | null | undefined): string {
    if (!resourceTypeId) return '—';
    return this.schemas().find(s => s.id === resourceTypeId)?.name ?? '—';
  }
}
