import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { ReserveComponentType } from '../models/enums';

/** Largest reserve amount (by absolute value) the API accepts — mirrors ReserveLimits.MaxAmount. */
export const RESERVE_MAX_AMOUNT = 1_000_000_000;

/**
 * Sign and size rule for reserve amounts (mirrors the API's Open/Adjust/CreateClaim validators):
 * RecoveryReserve amounts must be negative, every other component's must be positive, and no
 * amount may exceed RESERVE_MAX_AMOUNT in absolute value.
 *
 * The component type is read from a sibling control (default name "componentType"), so the
 * validator can sit on the amount control; re-run it when the type changes. Empty values are left
 * to Validators.required.
 */
export function reserveAmountSignValidator(componentTypeControlName = 'componentType'): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const raw = control.value;
    if (raw === null || raw === undefined || raw === '') return null;

    const amount = Number(raw);
    if (Number.isNaN(amount)) return null;
    if (Math.abs(amount) > RESERVE_MAX_AMOUNT) return { amountTooLarge: { max: RESERVE_MAX_AMOUNT } };

    const componentType = control.parent?.get(componentTypeControlName)?.value as ReserveComponentType | undefined;
    if (componentType === ReserveComponentType.RecoveryReserve) {
      return amount < 0 ? null : { recoveryMustBeNegative: true };
    }
    return amount > 0 ? null : { mustBePositive: true };
  };
}

/** User-facing text for the reserve-amount sign/size errors, or null when the errors are something else. */
export function reserveAmountSignMessage(errors: ValidationErrors | null | undefined): string | null {
  if (errors?.['amountTooLarge']) return `Reserve amount cannot exceed ${RESERVE_MAX_AMOUNT.toLocaleString('en-US')}.`;
  if (errors?.['recoveryMustBeNegative']) return 'Recovery reserve amounts must be negative.';
  if (errors?.['mustBePositive']) return 'Reserve amount must be greater than zero.';
  return null;
}
