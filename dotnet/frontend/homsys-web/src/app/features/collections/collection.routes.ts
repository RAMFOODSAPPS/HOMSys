import { Routes } from '@angular/router';

const ANY = ['collections-encode', 'collections-verify', 'collections-manage'];

export const collectionRoutes: Routes = [
  {
    path: '',
    // Custodians encode here; finance opens saved collections here (read-only, plus Verify / Return).
    data: { permission: ANY },
    loadComponent: () =>
      import('./collection-page/collection-page.component').then(m => m.CollectionPageComponent)
  },
  {
    path: 'list',
    // Custodians see their branches; finance (verify / manage) sees all.
    data: { permission: ANY },
    loadComponent: () =>
      import('./collection-list-page/collection-list-page.component').then(m => m.CollectionListPageComponent)
  },
  {
    path: 'dashboard',
    // Opens the shared "Collections Overview" Data Analytics dashboard.
    data: { permission: 'data-analytics' },
    loadComponent: () =>
      import('./collection-dashboard-redirect.component').then(m => m.CollectionDashboardRedirectComponent)
  },
  {
    path: 'bank',
    // Finance Manager uploads and matches; verifiers read.
    data: { permission: ['collections-manage', 'collections-verify'] },
    loadComponent: () =>
      import('./bank-statement-page/bank-statement-page.component').then(m => m.BankStatementPageComponent)
  },
  {
    path: 'remittance',
    // Remittance Advice (NKA): the Custodian Verifier encodes; finance opens it to verify / return it as a whole.
    data: { permission: ['collections-ra-encode', 'collections-verify', 'collections-manage'] },
    loadComponent: () =>
      import('./remittance-page/remittance-page.component').then(m => m.RemittancePageComponent)
  },
  {
    path: 'verify',
    data: { permission: 'collections-verify' },
    loadComponent: () =>
      import('./collection-verify-page/collection-verify-page.component').then(m => m.CollectionVerifyPageComponent)
  }
];
