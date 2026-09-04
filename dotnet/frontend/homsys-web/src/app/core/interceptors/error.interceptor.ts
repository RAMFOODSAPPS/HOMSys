import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError, finalize, shareReplay, switchMap } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';

// Shared in-flight refresh, not a boolean flag: several authenticated calls can
// 401 concurrently (e.g. on page load once the token's expired), and each one
// needs to await/reuse the SAME refresh instead of skipping its own retry
// because another request already flipped a "refreshing" switch.
let refreshInProgress$: Observable<unknown> | null = null;

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401 && !req.url.includes('/auth/')) {
        if (!refreshInProgress$) {
          refreshInProgress$ = auth.refresh().pipe(
            finalize(() => refreshInProgress$ = null),
            shareReplay(1)
          );
        }
        return (refreshInProgress$ as Observable<unknown>).pipe(
          switchMap(() => next(req.clone({ setHeaders: { Authorization: `Bearer ${auth.getAccessToken()}` }, withCredentials: true }))),
          catchError(e => { auth.logout(); return throwError(() => e); })
        );
      }
      return throwError(() => err);
    })
  );
};
