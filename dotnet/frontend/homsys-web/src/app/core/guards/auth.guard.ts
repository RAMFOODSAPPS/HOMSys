import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  // Keep where the user was going (e.g. an email's "Open in HOMSys" link) so login can return there.
  if (!auth.isAuthenticated())
    return router.createUrlTree(['/login'], state.url && state.url !== '/' ? { queryParams: { returnUrl: state.url } } : {});
  if (auth.user()?.mustChangePassword) return router.parseUrl('/change-password');
  return true;
};
