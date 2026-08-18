import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { TokenService } from './token.service';
import { SnackbarService } from '../../shared/snackbar.service';

/**
 * Attaches the bearer token to gateway API calls and, on a 401, clears the session
 * and routes back to login. The token endpoint is left untouched — it authenticates
 * with form credentials, not a bearer.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const tokenService = inject(TokenService);
  const router = inject(Router);
  const snackbar = inject(SnackbarService);

  const token = tokenService.token();
  const authed =
    token && req.url.startsWith('/api')
      ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : req;

  return next(authed).pipe(
    catchError((error) => {
      if (error.status === 401) {
        tokenService.clear();
        snackbar.error('Session expired — please sign in again.');
        void router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
      }
      return throwError(() => error);
    }),
  );
};
