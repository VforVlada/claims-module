import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { SnackbarService } from '../../shared/services/snackbar.service';
import { ApiErrorResponse } from '../../shared/models/common.models';
import { AuthService } from '../services/auth.service';
import { ERROR_HANDLED_BY_CALLER } from './error-handling-context';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const snackbar = inject(SnackbarService);
  const auth = inject(AuthService);
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && !req.context.get(ERROR_HANDLED_BY_CALLER)(error)) {
        if (error.status === 401) {
          auth.logout();
          router.navigate(['/login']);
          snackbar.error('Your session has expired. Please sign in again.');
        } else {
          snackbar.error(describeError(error));
        }
      }

      return throwError(() => error);
    })
  );
};

/** Generic ProblemDetails titles ASP.NET emits for empty 4xx responses — not worth showing verbatim. */
const GENERIC_TITLES = new Set(['Bad Request', 'Forbidden', 'Not Found', 'Conflict', 'Unprocessable Entity']);

/**
 * Turns an API error (RFC 7807 ProblemDetails) into a message a claims handler can act on.
 * Server errors never surface raw details — only the trace id, so support can find the log entry.
 */
export function describeError(error: HttpErrorResponse): string {
  const body = typeof error.error === 'object' && error.error !== null ? (error.error as ApiErrorResponse) : undefined;
  const fieldMessages = body?.errors ? Object.values(body.errors).flat() : [];
  const title = body?.title && !GENERIC_TITLES.has(body.title) ? body.title : null;

  if (error.status === 0) {
    return 'Could not reach the Claims Module API. Check your connection and try again.';
  }

  if (error.status >= 500) {
    const reference = body?.traceId ? ` (reference: ${body.traceId})` : '';
    return `Something went wrong on our side. Please try again in a moment${reference}.`;
  }

  if (error.status === 403) {
    return title ? `You don't have permission to do that: ${title}` : "You don't have permission to perform this action.";
  }

  if (error.status === 400) {
    return fieldMessages.length > 0
      ? `Please correct the highlighted fields: ${fieldMessages.join(' ')}`
      : (title ?? 'The request was not valid. Please check your input and try again.');
  }

  if (error.status === 404) {
    return title ?? 'The requested record could not be found.';
  }

  if (fieldMessages.length > 0) {
    return fieldMessages.join(' ');
  }

  return title ?? `Request failed (${error.status}).`;
}
