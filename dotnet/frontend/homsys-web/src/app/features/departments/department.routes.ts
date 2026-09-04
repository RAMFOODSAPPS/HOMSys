import { Routes } from '@angular/router';

export const departmentRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./department-page/department-page.component').then(m => m.DepartmentPageComponent)
  },
  {
    path: 'list',
    loadComponent: () =>
      import('./department-list-page/department-list-page.component').then(m => m.DepartmentListPageComponent)
  }
];
