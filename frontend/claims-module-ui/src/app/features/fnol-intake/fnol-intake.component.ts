import { Component, OnInit, ViewChild, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AbstractControl, FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { Subscription, catchError, debounceTime, distinctUntilChanged, map, of, switchMap } from 'rxjs';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { toSignal } from '@angular/core/rxjs-interop';

import { MatStepper, MatStepperModule } from '@angular/material/stepper';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';

import { ClaimsService } from '../../core/services/claims.service';
import { PoliciesService } from '../../core/services/policies.service';
import { ReferenceDataService } from '../../core/services/reference-data.service';
import { SnackbarService } from '../../shared/services/snackbar.service';
import { CauseOfLossCodeDto } from '../../shared/models/reference-data.models';
import { PolicyCoverageDto, PolicySearchResultDto } from '../../shared/models/policy.models';
import {
  AssetType,
  AssetTypeLabels,
  ClaimType,
  ClaimTypeLabels,
  PartyRole,
  PartyRoleLabels,
  PartyType,
  PartyTypeLabels,
  PolicyStatus,
  PolicyStatusLabels,
  ReserveComponentType,
  ReserveComponentTypeLabels,
  ApprovalTierMessages,
  ApprovalTierName,
  estimateApprovalTier,
  optionsByLabel
} from '../../shared/models/enums';
import { CreateClaimRequest } from '../../shared/models/claim.models';
import { ApiErrorResponse } from '../../shared/models/common.models';
import { notInFutureValidator } from '../../shared/validators/not-in-future.validator';
import { reserveAmountSignMessage, reserveAmountSignValidator } from '../../shared/validators/reserve-amount.validator';
import { PhoneInputDirective } from '../../shared/directives/phone-input.directive';
import { CurrencyInputDirective } from '../../shared/directives/currency-input.directive';
import { contactEmailMessage, contactEmailValidator } from '../../shared/validators/email.validator';
import { FIELD_LIMITS } from '../../shared/models/field-limits';

/** Shown in place of the wizard once the claim has been created. */
export interface CreatedClaimBanner {
  id: string;
  claimNumber: string;
  warnings: string[];
}

@Component({
  selector: 'app-fnol-intake',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    MatStepperModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatAutocompleteModule,
    MatCheckboxModule,
    MatSlideToggleModule,
    PhoneInputDirective,
    CurrencyInputDirective
  ],
  templateUrl: './fnol-intake.component.html',
  styleUrl: './fnol-intake.component.scss'
})
export class FnolIntakeComponent implements OnInit {
  readonly limits = FIELD_LIMITS;
  readonly claimTypes = optionsByLabel(ClaimTypeLabels);
  readonly claimTypeLabels = ClaimTypeLabels;
  readonly PolicyStatus = PolicyStatus;
  readonly policyStatusLabels = PolicyStatusLabels;
  readonly partyTypes = optionsByLabel(PartyTypeLabels);
  readonly partyTypeLabels = PartyTypeLabels;
  readonly partyRoles = optionsByLabel(PartyRoleLabels);
  readonly partyRoleLabels = PartyRoleLabels;
  readonly assetTypes = optionsByLabel(AssetTypeLabels);
  readonly assetTypeLabels = AssetTypeLabels;
  readonly reserveComponentTypes = optionsByLabel(ReserveComponentTypeLabels);
  readonly reserveComponentTypeLabels = ReserveComponentTypeLabels;
  readonly approvalTierMessages = ApprovalTierMessages;
  readonly PartyRole = PartyRole;

  readonly causeOfLossCodes = signal<CauseOfLossCodeDto[]>([]);
  readonly policyResults = signal<PolicySearchResultDto[]>([]);
  /** A 2+ character search came back empty — shown in the dropdown so the user knows nothing was linked. */
  readonly policyNoMatches = signal(false);
  readonly selectedPolicy = signal<PolicySearchResultDto | null>(null);
  /** Brief §3.3.2: the picked policy's coverages, shown during FNOL so the handler sees what's covered. */
  readonly policyCoverages = signal<PolicyCoverageDto[]>([]);
  readonly submitting = signal(false);
  readonly createdClaim = signal<CreatedClaimBanner | null>(null);
  readonly today = new Date();

  readonly stepperOrientation;

  @ViewChild('stepper') private stepper?: MatStepper;
  private coverageRequest?: Subscription;

  step1Form: FormGroup;
  step2Form: FormGroup;
  step3Form: FormGroup;

  constructor(
    private readonly fb: FormBuilder,
    private readonly claimsService: ClaimsService,
    private readonly policiesService: PoliciesService,
    private readonly referenceData: ReferenceDataService,
    private readonly snackbar: SnackbarService,
    private readonly breakpointObserver: BreakpointObserver
  ) {
    this.stepperOrientation = toSignal(
      this.breakpointObserver
        .observe(Breakpoints.Handset)
        .pipe(map((result): 'vertical' | 'horizontal' => (result.matches ? 'vertical' : 'horizontal'))),
      { initialValue: 'horizontal' as const }
    );

    this.step1Form = this.buildStep1Form();
    this.step2Form = this.buildStep2Form();
    this.step3Form = this.buildStep3Form();
  }

  private buildStep1Form(): FormGroup {
    return this.fb.group({
      // Typed text alone links no policy: a result must be picked (the control then holds the policy
      // object), or "Unknown policy" switched on, which disables this control and so skips the check.
      policySearch: ['' as string | PolicySearchResultDto, (c: AbstractControl) => (c.value && typeof c.value === 'object' ? null : { policyRequired: true })],
      unknownPolicy: [false],
      assignedHandler: ['', Validators.required],
      claimType: [ClaimType.Auto, Validators.required],
      lossDate: [new Date(), [Validators.required, notInFutureValidator]],
      lossDescription: ['', [Validators.required, Validators.maxLength(2000)]],
      lossLocation: ['', [Validators.required, Validators.maxLength(500)]],
      causeOfLossCodeId: ['', Validators.required]
    });
  }

  private buildStep2Form(): FormGroup {
    return this.fb.group({
      parties: this.fb.array([]),
      riskObjects: this.fb.array([])
    });
  }

  private buildStep3Form(): FormGroup {
    const form = this.fb.group({
      hasInitialReserve: [false],
      componentType: [ReserveComponentType.IndemnityReserve],
      // Sign rule: RecoveryReserve must be negative, every other component positive (mirrors the API).
      amount: [null as number | null, reserveAmountSignValidator()],
      currency: ['USD']
    });
    form.get('componentType')!.valueChanges.subscribe(() => form.get('amount')!.updateValueAndValidity());

    // The reserve is optional, but once the toggle is on its amount and currency are required.
    const amount = form.get('amount')!;
    const currency = form.get('currency')!;
    form.get('hasInitialReserve')!.valueChanges.subscribe((on) => {
      if (on) {
        amount.addValidators(Validators.required);
        currency.addValidators([Validators.required, Validators.pattern(/^[A-Za-z]{3}$/)]);
      } else {
        amount.removeValidators(Validators.required);
        currency.clearValidators();
      }
      amount.updateValueAndValidity();
      currency.updateValueAndValidity();
    });
    return form;
  }

  /** Submit is only enabled once every required field across all three steps is filled in. */
  get canSubmit(): boolean {
    return this.step1Form.valid && this.step2Form.valid && this.hasClaimant && (!this.step3Form.value.hasInitialReserve || this.step3Form.valid);
  }

  get isRecoveryReserve(): boolean {
    return this.step3Form.get('componentType')?.value === ReserveComponentType.RecoveryReserve;
  }

  claimTypeLabel(type: ClaimType): string {
    return this.claimTypeLabels[type];
  }

  ngOnInit(): void {
    this.referenceData.listCauseOfLossCodes().subscribe((codes) => this.causeOfLossCodes.set(codes));
    this.wireStep1Form();
    this.addParty();
  }

  private wireStep1Form(): void {
    // "Unknown policy" disables the search box (via the control, not a [disabled] binding on a reactive input).
    const policySearch = this.step1Form.get('policySearch')!;
    this.step1Form.get('unknownPolicy')!.valueChanges.subscribe((unknown: boolean) =>
      unknown ? policySearch.disable({ emitEvent: false }) : policySearch.enable({ emitEvent: false })
    );

    // The control holds typed text while searching and the chosen policy object once one is picked;
    // typing again after a pick drops that selection so a stale policy can't be submitted.
    policySearch.valueChanges.subscribe((value: string | PolicySearchResultDto | null) => {
      if (typeof value === 'string' && this.selectedPolicy()) this.setPolicy(null);
    });

    // Policy typeahead: one search request once typing pauses for 300 ms, and only for 2+ characters.
    // A failed search is swallowed inside switchMap (the error interceptor has already shown the
    // snackbar) so the stream survives and the next keystroke searches again. It reports no term,
    // so a failure isn't mistaken for "no matching policies".
    policySearch.valueChanges
      .pipe(
        map((value: string | PolicySearchResultDto | null) => (typeof value === 'string' ? value : '')),
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((term) =>
          term.length >= 2
            ? this.policiesService.search(term).pipe(
                map((results) => ({ term, results })),
                catchError(() => of({ term: '', results: [] as PolicySearchResultDto[] }))
              )
            : of({ term, results: [] })
        )
      )
      .subscribe(({ term, results }) => {
        this.policyResults.set(results);
        this.policyNoMatches.set(term.length >= 2 && results.length === 0);
      });
  }

  /** How the search box shows a picked policy (the control's value is the policy object itself). */
  readonly policyLabel = (value: string | PolicySearchResultDto | null): string =>
    value && typeof value === 'object' ? `${value.policyNumber} — ${value.clientName}` : (value ?? '');

  get parties(): FormArray {
    return this.step2Form.get('parties') as FormArray;
  }

  get riskObjects(): FormArray {
    return this.step2Form.get('riskObjects') as FormArray;
  }

  get hasClaimant(): boolean {
    return this.parties.controls.some((c) => c.get('partyRole')?.value === PartyRole.Claimant);
  }

  get estimatedTier(): ApprovalTierName | '' {
    const amount = this.step3Form.get('amount')?.value;
    if (!amount) return '';
    return estimateApprovalTier(Math.abs(amount));
  }

  selectPolicy(policy: PolicySearchResultDto): void {
    this.setPolicy(policy);
    this.step1Form.patchValue({ unknownPolicy: false });
  }

  coverageSummary(): string {
    return this.policyCoverages()
      .map((c) => c.coverageType)
      .join(', ');
  }

  /** Single place the picked policy changes, so its coverages always match it. */
  private setPolicy(policy: PolicySearchResultDto | null): void {
    this.selectedPolicy.set(policy);
    this.policyCoverages.set([]);
    this.coverageRequest?.unsubscribe();
    if (policy) {
      this.coverageRequest = this.policiesService.getCoverage(policy.policyId).subscribe((coverages) => {
        if (this.selectedPolicy()?.policyId === policy.policyId) this.policyCoverages.set(coverages);
      });
    }
  }

  clearPolicy(): void {
    this.setPolicy(null);
    this.step1Form.patchValue({ policySearch: '' });
  }

  addParty(): void {
    this.parties.push(
      this.fb.group({
        partyType: [PartyType.Individual, Validators.required],
        partyRole: [PartyRole.Claimant, Validators.required],
        name: ['', Validators.required],
        contactEmail: ['', contactEmailValidator()],
        contactPhone: ['']
      })
    );
  }

  removeParty(index: number): void {
    this.parties.removeAt(index);
  }

  addRiskObject(): void {
    this.riskObjects.push(
      this.fb.group({
        assetType: [AssetType.Vehicle, Validators.required],
        description: ['', Validators.required],
        identifier: ['']
      })
    );
  }

  removeRiskObject(index: number): void {
    this.riskObjects.removeAt(index);
  }

  submit(): void {
    if (this.step1Form.invalid || this.step2Form.invalid || !this.hasClaimant) {
      this.snackbar.warning('Please complete all required fields, including at least one Claimant.');
      return;
    }

    const step3 = this.step3Form.value;
    if (step3.hasInitialReserve && this.step3Form.invalid) {
      this.step3Form.markAllAsTouched();
      this.snackbar.warning('Please correct the initial reserve before submitting.');
      return;
    }

    const step1 = this.step1Form.value;

    const request: CreateClaimRequest = {
      policyId: step1.unknownPolicy ? null : (this.selectedPolicy()?.policyId ?? null),
      claimType: step1.claimType,
      lossDate: (step1.lossDate as Date).toISOString(),
      lossDescription: step1.lossDescription,
      lossLocation: step1.lossLocation,
      causeOfLossCodeId: step1.causeOfLossCodeId,
      assignedHandler: step1.assignedHandler,
      parties: this.parties.value,
      riskObjects: this.riskObjects.value,
      initialReserve: step3.hasInitialReserve
        ? { componentType: step3.componentType, amount: step3.amount, currency: step3.currency }
        : null
    };

    this.submitting.set(true);
    this.claimsService.create(request).subscribe({
      next: (result) => {
        this.submitting.set(false);
        this.createdClaim.set({
          id: result.value.id,
          claimNumber: result.value.claimNumber,
          warnings: (result.warnings ?? []).map((w) => w.message)
        });
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        this.applyServerErrors(error);
      }
    });
  }

  /** Resets the wizard after a successful submit so another claim can be logged. */
  startNewClaim(): void {
    this.step1Form = this.buildStep1Form();
    this.step2Form = this.buildStep2Form();
    this.step3Form = this.buildStep3Form();
    this.setPolicy(null);
    this.policyResults.set([]);
    this.policyNoMatches.set(false);
    this.wireStep1Form();
    this.addParty();
    this.createdClaim.set(null);
  }

  /** Text for a control's mat-error; a server-side validation message wins over client-side ones. */
  fieldError(control: AbstractControl | null): string {
    const errors = control?.errors;
    if (!errors) return '';
    if (errors['server']) return errors['server'] as string;
    if (errors['futureDate']) return 'Loss date cannot be in the future.';
    if (errors['policyRequired']) return 'Pick a policy from the list, or turn on "Unknown policy".';
    const reserveSign = reserveAmountSignMessage(errors);
    if (reserveSign) return reserveSign;
    const email = contactEmailMessage(errors);
    if (email) return email;
    if (errors['required']) return 'This field is required.';
    if (errors['maxlength']) return 'This value is too long.';
    if (errors['pattern']) return 'Use a 3-letter currency code, e.g. USD.';
    return 'This value is not valid.';
  }

  /**
   * Maps a 400 ProblemDetails `errors` dictionary — keyed by command property name, e.g.
   * "LossDate", "Parties", "Parties[0].Name", "InitialReserve.Amount" — onto the matching form
   * controls as a `server` error, then moves the stepper to the earliest step that has one.
   */
  private applyServerErrors(error: unknown): void {
    if (!(error instanceof HttpErrorResponse)) return;
    const errors = (error.error as ApiErrorResponse | null)?.errors;
    if (!errors) return;

    let firstStep: number | null = null;
    for (const [key, messages] of Object.entries(errors)) {
      const target = this.resolveServerField(key);
      if (!target) continue;

      target.control.setErrors({ ...(target.control.errors ?? {}), server: messages.join(' ') });
      target.control.markAsTouched();
      firstStep = firstStep === null ? target.step : Math.min(firstStep, target.step);
    }

    if (firstStep !== null && this.stepper) {
      this.stepper.selectedIndex = firstStep;
    }
  }

  private resolveServerField(key: string): { control: AbstractControl; step: number } | null {
    const path = key
      .replace(/\[(\d+)\]/g, '.$1')
      .split('.')
      .filter((segment) => segment.length > 0)
      .map((segment) => segment.charAt(0).toLowerCase() + segment.slice(1));

    // The initial reserve's fields live flat on step 3 (InitialReserve.Amount -> step3Form.amount).
    if (path[0] === 'initialReserve') path.shift();
    if (path.length === 0) return null;

    const forms = [this.step1Form, this.step2Form, this.step3Form];
    for (let step = 0; step < forms.length; step++) {
      const control = forms[step].get(path);
      if (control) return { control, step };
    }
    return null;
  }
}
