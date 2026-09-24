import { Directive, ElementRef, HostListener, Optional, Self } from '@angular/core';
import { NgControl } from '@angular/forms';

/** Keeps only letters, upper-cased, at most three (an ISO 4217 code such as "USD"). */
export function sanitizeCurrency(value: string): string {
  return value.replace(/[^A-Za-z]/g, '').toUpperCase().slice(0, 3);
}

/**
 * Restricts a text input to a currency code: letters only, upper-cased, max 3 (mirrors the API's
 * CurrencyRules.Pattern). Filters as the user types or pastes and writes the cleaned value back to
 * the bound form control.
 */
@Directive({
  selector: 'input[appCurrencyInput]',
  standalone: true,
  host: { maxlength: '3', autocomplete: 'off', style: 'text-transform: uppercase' }
})
export class CurrencyInputDirective {
  constructor(
    private readonly el: ElementRef<HTMLInputElement>,
    @Optional() @Self() private readonly ngControl: NgControl | null
  ) {}

  @HostListener('input')
  onInput(): void {
    const input = this.el.nativeElement;
    const cleaned = sanitizeCurrency(input.value);
    if (cleaned === input.value) return;
    input.value = cleaned;
    this.ngControl?.control?.setValue(cleaned, { emitEvent: true });
  }
}
