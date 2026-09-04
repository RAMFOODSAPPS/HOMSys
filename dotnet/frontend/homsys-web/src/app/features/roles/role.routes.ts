import { Routes } from '@angular/router';

export const roleRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./role-page/role-page.component').then(m => m.RolePageComponent)
  },
  {
    path: 'list',
    loadComponent: () =>
      import('./role-list-page/role-list-page.component').then(m => m.RoleListPageComponent)
  }
];
