import { HttpContextToken, HttpErrorResponse } from '@angular/common/http';
import { ApiErrorResponse } from '../../shared/models/common.models';

/**
 * Lets a request declare which of its errors the caller handles itself, so the global error
 * interceptor doesn't also show a snackbar for them. Defaults to "none": every error is shown.
 */
export const ERROR_HANDLED_BY_CALLER = new HttpContextToken<(error: HttpErrorResponse) => boolean>(() => () => false);

/** BR-R-07: the API answers 422 with a title naming the $10,000,000 cap when an approval would breach it. */
export function isAggregateCapRejection(error: unknown): boolean {
  if (!(error instanceof HttpErrorResponse) || error.status !== 422) return false;
  const title = (error.error as ApiErrorResponse | null)?.title ?? '';
  return title.includes('$10,000,000');
}
