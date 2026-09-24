import { AbstractControl, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';

/** Matches the ClaimParties.ContactEmail column length. */
export const EMAIL_MAX_LENGTH = 255;

/**
 * Optional contact email: empty is fine, otherwise it must be a well-formed address whose domain has
 * a dot (Angular's Validators.email alone accepts "name@host"). Stricter than the API's check, so
 * anything that passes here is also accepted server-side.
 */
export function contactEmailValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = ((control.value as string | null) ?? '').trim();
    if (!value) return null;
    if (value.length > EMAIL_MAX_LENGTH) return { emailTooLong: { max: EMAIL_MAX_LENGTH } };
    if (Validators.email(control) || !/@[^@\s]+\.[^@\s.]+$/.test(value)) return { email: true };
    return null;
  };
}

/** User-facing text for the contact email errors, or null when the errors are something else. */
export function contactEmailMessage(errors: ValidationErrors | null | undefined): string | null {
  if (errors?.['emailTooLong']) return `Email must be ${EMAIL_MAX_LENGTH} characters or fewer.`;
  if (errors?.['email']) return 'Enter a valid email address, e.g. name@company.com.';
  return null;
}
