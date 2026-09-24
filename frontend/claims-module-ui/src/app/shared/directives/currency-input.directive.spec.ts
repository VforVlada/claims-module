import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { CurrencyInputDirective, sanitizeCurrency } from './currency-input.directive';

describe('sanitizeCurrency', () => {
  it('keeps only letters, upper-cased, at most three', () => {
    expect(sanitizeCurrency('usd')).toBe('USD');
    expect(sanitizeCurrency('U1S$D')).toBe('USD');
    expect(sanitizeCurrency('123')).toBe('');
    expect(sanitizeCurrency('euro')).toBe('EUR');
  });
});

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, CurrencyInputDirective],
  template: `<input appCurrencyInput [formControl]="currency" />`
})
class HostComponent {
  readonly currency = new FormControl('');
}

describe('CurrencyInputDirective', () => {
  it('filters typed/pasted input and writes the cleaned value to the form control', () => {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');

    input.value = 'e1u2r';
    input.dispatchEvent(new Event('input'));

    expect(input.value).toBe('EUR');
    expect(fixture.componentInstance.currency.value).toBe('EUR');
  });
});
