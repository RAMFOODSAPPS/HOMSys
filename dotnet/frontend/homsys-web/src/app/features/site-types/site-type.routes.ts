import { Routes } from '@angular/router';

export const siteTypeRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./site-type-page/site-type-page.component').then(m => m.SiteTypePageComponent)
  },
  {
    path: 'list',
    loadComponent: () =>
      import('./site-type-list-page/site-type-list-page.component').then(m => m.SiteTypeListPageComponent)
  }
];
