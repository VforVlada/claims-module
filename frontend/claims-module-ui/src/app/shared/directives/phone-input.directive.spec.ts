import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { PhoneInputDirective, sanitizePhone } from './phone-input.directive';

describe('sanitizePhone', () => {
  it('keeps digits and a single leading "+"', () => {
    expect(sanitizePhone('+380501234567')).toBe('+380501234567');
    expect(sanitizePhone('0501234567')).toBe('0501234567');
  });

  it('strips letters, spaces, dashes, brackets and any non-leading "+"', () => {
    expect(sanitizePhone('+38 (050) 123-45-67')).toBe('+380501234567');
    expect(sanitizePhone('abc12x3')).toBe('123');
    expect(sanitizePhone('38+050')).toBe('38050');
    expect(sanitizePhone('++12')).toBe('+12');
  });
});

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, PhoneInputDirective],
  template: `<input appPhoneInput [formControl]="phone" />`
})
class HostComponent {
  readonly phone = new FormControl('');
}

describe('PhoneInputDirective', () => {
  it('filters typed/pasted input and writes the cleaned value to the form control', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');

    input.value = '+1 (555) abc-0100';
    input.dispatchEvent(new Event('input'));

    expect(input.value).toBe('+15550100');
    expect(fixture.componentInstance.phone.value).toBe('+15550100');
    expect(input.type).toBe('tel');
  });
});
