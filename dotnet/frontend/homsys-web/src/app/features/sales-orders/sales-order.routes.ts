import { Routes } from '@angular/router';

export const salesOrderRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./sales-order-page/sales-order-page.component')
        .then(m => m.SalesOrderPageComponent)
  },
  {
    path: 'list',
    loadComponent: () =>
      import('./sales-order-list-page/sales-order-list-page.component')
        .then(m => m.SalesOrderListPageComponent)
  }
];
