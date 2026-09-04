import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { routeRoleGuard } from './core/guards/role.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: '',
    canActivate: [authGuard],
    // routeRoleGuard auto-applies to every child route.
    // New routes: add data: { roles: ['Admin'] } for role-restricted pages,
    // or omit data.roles for auth-only (any logged-in user).
    canActivateChild: [routeRoleGuard],
    loadComponent: () =>
      import('./layout/app-layout/app-layout.component').then(m => m.AppLayoutComponent),
    children: [
      { path: '', redirectTo: 'home', pathMatch: 'full' },
      {
        path: 'home',
        loadComponent: () =>
          import('./features/dashboard/dashboard-page.component').then(m => m.DashboardPageComponent)
      },
      {
        path: 'unauthorized',
        loadComponent: () =>
          import('./features/auth/unauthorized/unauthorized.component').then(m => m.UnauthorizedComponent)
      },
      {
        path: 'users',
        data: { permission: 'users' },
        loadChildren: () =>
          import('./features/users/user.routes').then(m => m.userRoutes)
      },
      {
        path: 'roles',
        data: { permission: 'roles' },
        loadChildren: () =>
          import('./features/roles/role.routes').then(m => m.roleRoutes)
      },
      {
        path: 'authorization',
        data: { permission: 'authorization' },
        loadComponent: () =>
          import('./features/authorization/authorization.component').then(m => m.AuthorizationComponent)
      },
      {
        path: 'companies',
        data: { permission: 'companies' },
        loadChildren: () =>
          import('./features/companies/company.routes').then(m => m.companyRoutes)
      },
      {
        path: 'departments',
        data: { permission: 'departments' },
        loadChildren: () =>
          import('./features/departments/department.routes').then(m => m.departmentRoutes)
      },
      {
        path: 'sites',
        data: { permission: 'sites' },
        loadChildren: () =>
          import('./features/sites/site.routes').then(m => m.siteRoutes)
      },
      {
        path: 'site-types',
        data: { permission: 'site-types' },
        loadChildren: () =>
          import('./features/site-types/site-type.routes').then(m => m.siteTypeRoutes)
      },
      {
        path: 'sales-orders',
        data: { permission: 'sales-orders' },
        loadChildren: () =>
          import('./features/sales-orders/sales-order.routes').then(m => m.salesOrderRoutes)
      },
      {
        path: 'oos-report',
        data: { permission: 'oos-report' },
        loadComponent: () =>
          import('./features/oos-report/oos-report-page.component').then(m => m.OosReportPageComponent)
      },
      {
        path: 'pricelist-export',
        data: { permission: 'pricelist-export' },
        loadComponent: () =>
          import('./features/pricelist-export/pricelist-export-page.component').then(m => m.PricelistExportPageComponent)
      },
      {
        path: 'legacy-monitoring',
        data: { permission: 'legacy-monitoring' },
        loadComponent: () =>
          import('./features/legacy-monitoring/legacy-monitoring-page.component').then(m => m.LegacyMonitoringPageComponent)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
