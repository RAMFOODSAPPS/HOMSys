import { Routes } from '@angular/router';

export const userRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./user-page/user-page.component').then(m => m.UserPageComponent)
  },
  {
    path: 'list',
    loadComponent: () =>
      import('./user-list-page/user-list-page.component').then(m => m.UserListPageComponent)
  }
];
