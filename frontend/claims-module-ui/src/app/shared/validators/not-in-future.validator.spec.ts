import { FormControl } from '@angular/forms';
import { notInFutureValidator } from './not-in-future.validator';

describe('notInFutureValidator (F-04)', () => {
  const check = (value: unknown) => notInFutureValidator(new FormControl(value));

  it('accepts now and past dates', () => {
    expect(check(new Date())).toBeNull();
    expect(check(new Date(2020, 0, 1))).toBeNull();
    expect(check('2020-01-01T00:00:00Z')).toBeNull();
  });

  it('rejects future dates', () => {
    expect(check(new Date(Date.now() + 60_000))).toEqual({ futureDate: true });
    expect(check(new Date(new Date().getFullYear() + 1, 0, 1))).toEqual({ futureDate: true });
  });

  it('leaves empty or unparseable values to other validators', () => {
    expect(check(null)).toBeNull();
    expect(check('')).toBeNull();
    expect(check('not a date')).toBeNull();
  });
});
