import { Directive, ElementRef, HostListener, Optional, Self } from '@angular/core';
import { NgControl } from '@angular/forms';

/** Strips everything but digits, keeping a "+" only as the first character. */
export function sanitizePhone(value: string): string {
  const leadingPlus = value.trimStart().startsWith('+');
  const digits = value.replace(/\D/g, '');
  return leadingPlus ? `+${digits}` : digits;
}

/**
 * Restricts a text input to a phone number: digits plus an optional leading "+" (mirrors the API's
 * ContactRules.PhonePattern). Filters as the user types or pastes, and writes the cleaned value back
 * to the bound form control so the model never holds a disallowed character.
 */
@Directive({
  selector: 'input[appPhoneInput]',
  standalone: true,
  host: { type: 'tel', inputmode: 'tel', autocomplete: 'tel', maxlength: '50' }
})
export class PhoneInputDirective {
  constructor(
    private readonly el: ElementRef<HTMLInputElement>,
    @Optional() @Self() private readonly ngControl: NgControl | null
  ) {}

  @HostListener('input')
  onInput(): void {
    const input = this.el.nativeElement;
    const cleaned = sanitizePhone(input.value);
    if (cleaned === input.value) return;

    // Keep the caret where the user was typing, shifted left by the characters removed before it.
    const caret = input.selectionStart ?? cleaned.length;
    const removedBeforeCaret = input.value.slice(0, caret).length - sanitizePhone(input.value.slice(0, caret)).length;
    input.value = cleaned;
    const position = Math.max(0, caret - removedBeforeCaret);
    input.setSelectionRange(position, position);

    this.ngControl?.control?.setValue(cleaned, { emitEvent: true });
  }
}
