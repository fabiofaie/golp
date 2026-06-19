import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

const AUTH_ENDPOINTS = ['/auth/login', '/auth/register', '/auth/refresh'];

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const token = authService.getToken();
  const authedReq = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  const isAuthEndpoint = AUTH_ENDPOINTS.some(path => req.url.includes(path));
  if (isAuthEndpoint || !token) {
    return next(authedReq);
  }

  return next(authedReq).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401 && authService.getRefreshToken()) {
        return authService.refresh().pipe(
          switchMap(() => {
            const retried = req.clone({ setHeaders: { Authorization: `Bearer ${authService.getToken()}` } });
            return next(retried);
          }),
          catchError(refreshError => {
            authService.logout();
            return throwError(() => refreshError);
          })
        );
      }
      return throwError(() => error);
    })
  );
};
