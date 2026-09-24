import { FormControl } from '@angular/forms';
import { contactEmailMessage, contactEmailValidator } from './email.validator';

describe('contactEmailValidator', () => {
  const check = (value: string | null) => contactEmailValidator()(new FormControl(value));

  it('allows an empty value (the email is optional)', () => {
    expect(check(null)).toBeNull();
    expect(check('')).toBeNull();
    expect(check('   ')).toBeNull();
  });

  it('accepts well-formed addresses', () => {
    expect(check('john.doe@example.com')).toBeNull();
    expect(check('a+tag@mail.co.uk')).toBeNull();
  });

  it('rejects malformed addresses', () => {
    for (const bad of ['john', 'john@', '@example.com', 'john@example', 'john@@example.com', 'john doe@example.com', 'john@example.']) {
      expect(check(bad)).withContext(bad).toEqual({ email: true });
    }
  });

  it('rejects addresses longer than the column', () => {
    expect(check(`${'a'.repeat(250)}@example.com`)).toEqual({ emailTooLong: { max: 255 } });
  });

  it('maps the errors to messages', () => {
    expect(contactEmailMessage({ email: true })).toBe('Enter a valid email address, e.g. name@company.com.');
    expect(contactEmailMessage({ emailTooLong: { max: 255 } })).toBe('Email must be 255 characters or fewer.');
    expect(contactEmailMessage({ required: true })).toBeNull();
  });
});
