import { Routes } from '@angular/router';

export const siteRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./site-page/site-page.component').then(m => m.SitePageComponent)
  },
  {
    path: 'list',
    loadComponent: () =>
      import('./site-list-page/site-list-page.component').then(m => m.SiteListPageComponent)
  }
];
