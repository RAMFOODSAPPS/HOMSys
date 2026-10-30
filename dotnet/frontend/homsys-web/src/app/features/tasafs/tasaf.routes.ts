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
  },
  {
    // LF Breakdown / Near Ex worksheets, attached to a TASAF (same 'tasaf' permission as the parent route).
    path: 'worksheets',
    loadComponent: () =>
      import('./tasaf-worksheet-page/tasaf-worksheet-page.component').then(m => m.TasafWorksheetPageComponent)
  }
];
