import { Routes } from '@angular/router';

export const tasafRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./tasaf-page/tasaf-page.component').then(m => m.TasafPageComponent)
  },
  {
    path: 'list',
    loadComponent: () =>
      import('./tasaf-list-page/tasaf-list-page.component').then(m => m.TasafListPageComponent)
  }
];
