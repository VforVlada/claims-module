import { FormControl, FormGroup } from '@angular/forms';
import { reserveAmountSignMessage, reserveAmountSignValidator } from './reserve-amount.validator';
import { ReserveComponentType } from '../models/enums';

describe('reserveAmountSignValidator', () => {
  function check(componentType: ReserveComponentType, amount: unknown) {
    const group = new FormGroup({
      componentType: new FormControl(componentType),
      amount: new FormControl(amount, reserveAmountSignValidator())
    });
    // A control validates once on construction, before it has a parent; re-run it inside the group.
    const amountControl = group.get('amount')!;
    amountControl.updateValueAndValidity();
    return amountControl.errors;
  }

  it('requires a positive amount for non-recovery components', () => {
    for (const type of [ReserveComponentType.IndemnityReserve, ReserveComponentType.ExpenseReserve, ReserveComponentType.LitigationReserve]) {
      expect(check(type, 100)).toBeNull();
      expect(check(type, '0.01')).toBeNull();
      expect(check(type, 0)).toEqual({ mustBePositive: true });
      expect(check(type, -5)).toEqual({ mustBePositive: true });
    }
  });

  it('requires a negative amount for RecoveryReserve', () => {
    expect(check(ReserveComponentType.RecoveryReserve, -2500)).toBeNull();
    expect(check(ReserveComponentType.RecoveryReserve, 0)).toEqual({ recoveryMustBeNegative: true });
    expect(check(ReserveComponentType.RecoveryReserve, 2500)).toEqual({ recoveryMustBeNegative: true });
  });

  it('rejects amounts above the maximum, in either direction', () => {
    expect(check(ReserveComponentType.IndemnityReserve, 1_000_000_000)).toBeNull();
    expect(check(ReserveComponentType.IndemnityReserve, 1_000_000_000.01)).toEqual({ amountTooLarge: { max: 1_000_000_000 } });
    expect(check(ReserveComponentType.IndemnityReserve, 1e20)).toEqual({ amountTooLarge: { max: 1_000_000_000 } });
    expect(check(ReserveComponentType.RecoveryReserve, -1e20)).toEqual({ amountTooLarge: { max: 1_000_000_000 } });
  });

  it('leaves empty values to Validators.required', () => {
    expect(check(ReserveComponentType.IndemnityReserve, null)).toBeNull();
    expect(check(ReserveComponentType.RecoveryReserve, '')).toBeNull();
  });

  it('treats a control without a component-type sibling as non-recovery', () => {
    expect(reserveAmountSignValidator()(new FormControl(-1))).toEqual({ mustBePositive: true });
  });

  it('maps the errors to messages', () => {
    expect(reserveAmountSignMessage({ recoveryMustBeNegative: true })).toBe('Recovery reserve amounts must be negative.');
    expect(reserveAmountSignMessage({ mustBePositive: true })).toBe('Reserve amount must be greater than zero.');
    expect(reserveAmountSignMessage({ amountTooLarge: { max: 1_000_000_000 } })).toBe('Reserve amount cannot exceed 1,000,000,000.');
    expect(reserveAmountSignMessage({ required: true })).toBeNull();
    expect(reserveAmountSignMessage(null)).toBeNull();
  });
});
