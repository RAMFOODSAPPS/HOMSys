import { Routes } from '@angular/router';

export const companyRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./company-page/company-page.component').then(m => m.CompanyPageComponent)
  },
  {
    path: 'list',
    loadComponent: () =>
      import('./company-list-page/company-list-page.component').then(m => m.CompanyListPageComponent)
  }
];
