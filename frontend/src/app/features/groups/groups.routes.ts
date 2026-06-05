import { Routes } from '@angular/router';

export const groupsRoutes: Routes = [
  {
    path: ':groupId',
    loadComponent: () =>
      import('./group-detail-page/group-detail-page.component').then(
        m => m.GroupDetailPageComponent,
      ),
  },
  {
    path: ':groupId/schemas',
    loadChildren: () =>
      import('../schemas/schemas.routes').then(m => m.schemasRoutes),
  },
  {
    path: ':groupId/resources/:resourceId',
    loadComponent: () =>
      import('./resource-detail-page/resource-detail-page.component').then(
        m => m.ResourceDetailPageComponent,
      ),
  },
];
