import { inject } from '@angular/core';
import { ActivatedRouteSnapshot, CanActivateChildFn, CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

// Per-route guard: roleGuard('Admin', 'Manager')
export const roleGuard = (...roles: string[]): CanActivateFn => () => {
  const auth = inject(AuthService);
  const user = auth.user();
  if (!user) return inject(Router).parseUrl('/login');
  const hasRole = roles.some(r => user.roles.includes(r));
  return hasRole ? true : inject(Router).parseUrl('/unauthorized');
};

// Child guard: reads required permission key from route data: { permission: 'users' }
// Apply as canActivateChild on the layout — automatically covers every new child route.
// New pages just declare data.permission; no per-route canActivate needed.
export const routeRoleGuard: CanActivateChildFn = (route: ActivatedRouteSnapshot) => {
  const auth = inject(AuthService);
  const user = auth.user();
  if (!user) return inject(Router).parseUrl('/login');

  const permission: string | undefined = route.data?.['permission'];
  if (!permission) return true; // no permission declared → auth-only route
  if (auth.isAdmin()) return true; // Admin bypasses all permission checks

  return auth.hasPermission(permission)
    ? true
    : inject(Router).parseUrl('/unauthorized');
};
