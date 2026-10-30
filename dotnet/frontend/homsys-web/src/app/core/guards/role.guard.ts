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

  // A string, or a list meaning "any of these" (e.g. a list page shared by encoders and finance).
  const permission: string | string[] | undefined = route.data?.['permission'];
  if (!permission || permission.length === 0) return true; // no permission declared → auth-only route
  if (auth.isAdmin()) return true; // Admin bypasses all permission checks

  return ([] as string[]).concat(permission).some(p => auth.hasPermission(p))
    ? true
    : inject(Router).parseUrl('/unauthorized');
};
