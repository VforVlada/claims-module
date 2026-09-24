import { AbstractControl, ValidationErrors } from '@angular/forms';

/** Client-side mirror of BR-C-01 ("Loss date cannot be in the future"). Empty values are left to Validators.required. */
export function notInFutureValidator(control: AbstractControl): ValidationErrors | null {
  const value = control.value as Date | string | null | undefined;
  if (!value) return null;

  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return null;

  return date.getTime() > Date.now() ? { futureDate: true } : null;
}
