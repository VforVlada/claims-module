import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { HarnessLoader, HarnessQuery, ComponentHarness } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { MatStepHarness, MatStepperHarness } from '@angular/material/stepper/testing';
import { MatFormFieldHarness } from '@angular/material/form-field/testing';
import { MatInputHarness } from '@angular/material/input/testing';
import { MatSelectHarness } from '@angular/material/select/testing';
import { MatDatepickerInputHarness } from '@angular/material/datepicker/testing';
import { MatButtonHarness } from '@angular/material/button/testing';
import { MatSlideToggleHarness } from '@angular/material/slide-toggle/testing';
import { MatAutocompleteHarness } from '@angular/material/autocomplete/testing';
import { MatChipHarness } from '@angular/material/chips/testing';

import { FnolIntakeComponent } from './fnol-intake.component';
import { SnackbarService } from '../../shared/services/snackbar.service';
import { ApprovalTierMessages, ApprovalTierName, PartyRole, PolicyStatus, ReserveComponentType } from '../../shared/models/enums';
import { CauseOfLossCodeDto } from '../../shared/models/reference-data.models';
import { API } from '../../testing/fixtures';

const STEP1 = 'Policy & Loss';
const STEP2 = 'Parties & Risk Objects';
const STEP3 = 'Reserve & Review';

const CODES: CauseOfLossCodeDto[] = [
  { id: 'col-1', code: 'COLL', description: 'Collision', perilCategory: 'Auto' },
  { id: 'col-2', code: 'FIRE', description: 'Fire', perilCategory: 'Property' }
];

type HarnessScope = Pick<HarnessLoader, 'getHarness' | 'getAllHarnesses'>;

describe('FnolIntakeComponent', () => {
  let fixture: ComponentFixture<FnolIntakeComponent>;
  let component: FnolIntakeComponent;
  let loader: HarnessLoader;
  let http: HttpTestingController;
  let snackbar: jasmine.SpyObj<SnackbarService>;

  beforeEach(() => {
    snackbar = jasmine.createSpyObj<SnackbarService>('SnackbarService', ['success', 'warning', 'error', 'show']);
    TestBed.configureTestingModule({
      imports: [FnolIntakeComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        { provide: SnackbarService, useValue: snackbar }
      ]
    });

    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(FnolIntakeComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    http.expectOne(`${API}/reference/cause-of-loss-codes`).flush(CODES);
    fixture.detectChanges();
    loader = TestbedHarnessEnvironment.loader(fixture);
  });

  afterEach(() => http.verify());

  // ---- helpers -------------------------------------------------------------------------------

  async function step(label: string): Promise<MatStepHarness> {
    const stepper = await loader.getHarness(MatStepperHarness);
    const [match] = await stepper.getSteps({ label });
    return match;
  }

  async function selectedStepLabel(): Promise<string> {
    const stepper = await loader.getHarness(MatStepperHarness);
    const [selected] = await stepper.getSteps({ selected: true });
    return selected.getLabel();
  }

  async function control<T extends ComponentHarness>(scope: HarnessScope, label: string | RegExp, type: HarnessQuery<T>): Promise<T> {
    const formField = await scope.getHarness(MatFormFieldHarness.with({ floatingLabelText: label }));
    const result = await formField.getControl(type as never);
    return result as unknown as T;
  }

  async function nextButton(stepLabel: string): Promise<MatButtonHarness> {
    return (await step(stepLabel)).getHarness(MatButtonHarness.with({ text: 'Next' }));
  }

  /** Fills step 1 through the UI, exactly as a user would. */
  async function fillStep1ViaUi(): Promise<void> {
    const s1 = await step(STEP1);
    await (await control(s1, 'Assigned Handler', MatInputHarness)).setValue('Hannah Handler');
    await (await control(s1, 'Loss Date', MatDatepickerInputHarness)).setValue('1/15/2025');
    const cause = await control(s1, 'Cause of Loss', MatSelectHarness);
    await cause.open();
    await cause.clickOptions({ text: 'Collision (Auto)' });
    await (await control(s1, 'Loss Location', MatInputHarness)).setValue('Main St & 5th Ave');
    await (await control(s1, 'Loss Description', MatInputHarness)).setValue('Rear-ended at a junction');
  }

  /** Programmatic setup for tests that exercise later steps. */
  function fillSteps1And2(): void {
    component.step1Form.patchValue({
      unknownPolicy: true,
      assignedHandler: 'Hannah Handler',
      lossDate: new Date(2025, 0, 15),
      lossDescription: 'Rear-ended at a junction',
      lossLocation: 'Main St',
      causeOfLossCodeId: 'col-1'
    });
    component.parties.at(0).patchValue({ name: 'Jane Claimant' });
    fixture.detectChanges();
  }

  async function goToStep3(): Promise<MatStepHarness> {
    fillSteps1And2();
    await (await step(STEP2)).select();
    await (await step(STEP3)).select();
    expect(await selectedStepLabel()).toBe(STEP3);
    return step(STEP3);
  }

  function text(): string {
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  // ---- F-04 ----------------------------------------------------------------------------------

  describe('F-04: step 1 validation', () => {
    it('starts invalid with Next disabled, and cannot advance', async () => {
      expect(await (await nextButton(STEP1)).isDisabled()).toBeTrue();

      await (await step(STEP2)).select();
      expect(await selectedStepLabel()).withContext('linear stepper blocks step 2').toBe(STEP1);
    });

    it('keeps Next disabled until a policy is picked or "Unknown policy" is on', async () => {
      await fillStep1ViaUi();
      const s1 = await step(STEP1);
      const search = await control(s1, /Search policy/, MatInputHarness);
      await search.setValue('4444'); // typed text that matches nothing links no policy
      await search.blur();
      http.match((r) => r.url === `${API}/policies/search`).forEach((r) => r.flush([]));

      expect(await (await nextButton(STEP1)).isDisabled()).toBeTrue();
      expect(await (await s1.getHarness(MatFormFieldHarness.with({ floatingLabelText: /Search policy/ }))).getTextErrors()).toEqual([
        'Pick a policy from the list, or turn on "Unknown policy".'
      ]);

      await (await s1.getHarness(MatSlideToggleHarness.with({ label: /Unknown policy/ }))).check();
      expect(await (await nextButton(STEP1)).isDisabled()).toBeFalse();
    });

    it('enables Next once every required field is filled', async () => {
      await fillStep1ViaUi();
      await (await (await step(STEP1)).getHarness(MatSlideToggleHarness.with({ label: /Unknown policy/ }))).check();
      expect(component.step1Form.valid).toBeTrue();

      const next = await nextButton(STEP1);
      expect(await next.isDisabled()).toBeFalse();
      await next.click();
      expect(await selectedStepLabel()).toBe(STEP2);
    });

    for (const label of ['Assigned Handler', 'Loss Location', 'Loss Description']) {
      it(`disables Next when "${label}" is missing`, async () => {
        await fillStep1ViaUi();
        const input = await control(await step(STEP1), label, MatInputHarness);
        await input.setValue('');
        await input.blur();

        expect(await (await nextButton(STEP1)).isDisabled()).toBeTrue();
        const formField = await (await step(STEP1)).getHarness(MatFormFieldHarness.with({ floatingLabelText: label }));
        expect(await formField.getTextErrors()).toEqual(['This field is required.']);
      });
    }

    it('disables Next when the cause of loss is missing', async () => {
      await fillStep1ViaUi();
      component.step1Form.patchValue({ causeOfLossCodeId: '' });
      fixture.detectChanges();
      expect(await (await nextButton(STEP1)).isDisabled()).toBeTrue();
    });

    it('rejects a future loss date with an inline error and Next disabled', async () => {
      await fillStep1ViaUi();
      const nextYear = new Date().getFullYear() + 1;
      const date = await control(await step(STEP1), 'Loss Date', MatDatepickerInputHarness);
      await date.setValue(`6/1/${nextYear}`);
      await date.blur();

      expect(component.step1Form.get('lossDate')!.hasError('futureDate')).toBeTrue();
      expect(await (await nextButton(STEP1)).isDisabled()).toBeTrue();
      const formField = await (await step(STEP1)).getHarness(MatFormFieldHarness.with({ floatingLabelText: 'Loss Date' }));
      expect(await formField.getTextErrors()).toEqual(['Loss date cannot be in the future.']);
    });

    it('caps the datepicker at today', async () => {
      const date = await control(await step(STEP1), 'Loss Date', MatDatepickerInputHarness);
      const now = new Date();
      const pad = (n: number) => String(n).padStart(2, '0');
      expect(await date.getMax()).toBe(`${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`);
    });
  });

  // ---- F-05 ----------------------------------------------------------------------------------

  describe('F-05: policy typeahead', () => {
    it('sends a single search request once typing stops (fakeAsync)', fakeAsync(() => {
      const search = component.step1Form.get('policySearch')!;

      // Simulate keystrokes 100 ms apart.
      for (const partial of ['P', 'PO', 'POL', 'POL-', 'POL-2026']) {
        search.setValue(partial);
        tick(100);
      }
      expect(http.match((r) => r.url === `${API}/policies/search`).length).withContext('still typing').toBe(0);

      tick(199);
      expect(http.match((r) => r.url === `${API}/policies/search`).length).withContext('299 ms after last key').toBe(0);

      tick(1);
      const requests = http.match((r) => r.url === `${API}/policies/search`);
      expect(requests.length).toBe(1);
      expect(requests[0].request.params.get('searchTerm')).toBe('POL-2026');
      requests[0].flush([]);
    }));

    it('"Unknown policy" disables the search box and clears any selection', async () => {
      const s1 = await step(STEP1);
      const input = await control(s1, /Search policy/, MatInputHarness);
      expect(await input.isDisabled()).toBeFalse();

      await (await s1.getHarness(MatSlideToggleHarness.with({ label: /Unknown policy/ }))).check();
      expect(await input.isDisabled()).toBeTrue();
      expect(component.selectedPolicy()).toBeNull();

      await (await s1.getHarness(MatSlideToggleHarness.with({ label: /Unknown policy/ }))).uncheck();
      expect(await input.isDisabled()).toBeFalse();
    });

    it('keeps searching after a failed request, without reporting "no matches" for the failure', fakeAsync(() => {
      const search = component.step1Form.get('policySearch')!;

      search.setValue('POL-1');
      tick(300);
      http.expectOne((r) => r.url === `${API}/policies/search`).flush(null, { status: 500, statusText: 'Server Error' });
      expect(component.policyNoMatches()).toBeFalse();

      search.setValue('POL-2');
      tick(300);
      const retry = http.expectOne((r) => r.url === `${API}/policies/search`);
      expect(retry.request.params.get('searchTerm')).toBe('POL-2');
      retry.flush([]);
      expect(component.policyNoMatches()).toBeTrue();
    }));

    it('does not search for terms shorter than 2 characters', fakeAsync(() => {
      component.step1Form.get('policySearch')!.setValue('P');
      tick(300);
      expect(http.match((r) => r.url === `${API}/policies/search`).length).toBe(0);
    }));

    it('typing through the input harness produces one request, and picking a result shows the policy badge', async () => {
      const s1 = await step(STEP1);
      const input = await control(s1, /Search policy/, MatInputHarness);
      await input.setValue('POL-2026'); // typed character by character

      const requests = http.match((r) => r.url === `${API}/policies/search`);
      expect(requests.length).toBe(1);
      expect(requests[0].request.params.get('searchTerm')).toBe('POL-2026');
      requests[0].flush([
        {
          policyId: 'pol-1',
          policyNumber: 'POL-2026-000001',
          clientName: 'Acme Corp',
          effectiveDate: '2026-01-01',
          expirationDate: '2026-12-31',
          status: PolicyStatus.Active
        }
      ]);

      const autocomplete = await s1.getHarness(MatAutocompleteHarness);
      await autocomplete.focus();
      await autocomplete.selectOption({ text: /POL-2026-000001/ });

      expect(component.selectedPolicy()?.policyId).toBe('pol-1');
      expect(text()).toContain('In force');
      // The picked policy stays visible in the search box (it used to be blanked right after picking).
      expect(await input.getValue()).toBe('POL-2026-000001 — Acme Corp');
      // Selecting fills the input, which must not trigger another search round-trip for the label text.
      expect(http.match((r) => r.url === `${API}/policies/search`).length).toBe(0);

      // Brief §3.3.2: the picked policy's coverages are fetched and shown during FNOL.
      http.expectOne(`${API}/policies/pol-1/coverage`).flush([
        { id: 'cov-1', coverageType: 'Collision', limit: 50_000, deductible: 500, currency: 'USD' },
        { id: 'cov-2', coverageType: 'Liability', limit: 1_000_000, deductible: 0, currency: 'USD' }
      ]);
      fixture.detectChanges();
      expect(text()).toContain('Coverages on this policy');
      expect(text()).toContain('Collision');
      expect(text()).toContain('$1,000,000.00');

      // Typing again drops the pick (and its coverages) so a stale policy can't be submitted.
      await input.setValue('Harbor');
      expect(component.selectedPolicy()).toBeNull();
      expect(component.policyCoverages()).toEqual([]);
      http.match((r) => r.url === `${API}/policies/search`).forEach((r) => r.flush([]));
    });
  });

  // ---- F-06 ----------------------------------------------------------------------------------

  describe('F-06: step 2 parties and risk objects', () => {
    beforeEach(async () => {
      fillSteps1And2();
      component.parties.at(0).patchValue({ name: '' });
      await (await step(STEP2)).select();
    });

    it('starts with one Claimant row whose name is required', async () => {
      const s2 = await step(STEP2);
      const names = await s2.getAllHarnesses(MatFormFieldHarness.with({ floatingLabelText: 'Name' }));
      expect(names.length).toBe(1);
      expect(await (await nextButton(STEP2)).isDisabled()).toBeTrue();

      await (await control(s2, 'Name', MatInputHarness)).setValue('Jane Claimant');
      expect(await (await nextButton(STEP2)).isDisabled()).toBeFalse();
    });

    it('is invalid until at least one party is a Claimant', async () => {
      const s2 = await step(STEP2);
      await (await control(s2, 'Name', MatInputHarness)).setValue('Walter Witness');
      const role = await control(s2, 'Role', MatSelectHarness);
      await role.open();
      await role.clickOptions({ text: 'Witness' });

      expect(component.hasClaimant).toBeFalse();
      expect(text()).toContain('At least one Claimant party is required');
      expect(await (await nextButton(STEP2)).isDisabled()).toBeTrue();

      await role.open();
      await role.clickOptions({ text: 'Claimant' });
      expect(text()).not.toContain('At least one Claimant party is required');
      expect(await (await nextButton(STEP2)).isDisabled()).toBeFalse();
    });

    it('adds and removes party rows (never below one)', async () => {
      const s2 = await step(STEP2);
      const removeButtons = () => s2.getAllHarnesses(MatButtonHarness.with({ selector: '[aria-label="Remove party"]' }));

      expect(await (await removeButtons())[0].isDisabled()).withContext('only row cannot be removed').toBeTrue();

      await (await s2.getHarness(MatButtonHarness.with({ text: /Add Party/ }))).click();
      expect(component.parties.length).toBe(2);
      expect((await s2.getAllHarnesses(MatFormFieldHarness.with({ floatingLabelText: 'Name' }))).length).toBe(2);

      const [first, second] = await removeButtons();
      expect(await first.isDisabled()).toBeFalse();
      await second.click();
      expect(component.parties.length).toBe(1);
      expect(await (await removeButtons())[0].isDisabled()).toBeTrue();
    });

    it('adds and removes risk objects, including via the chip remove button', async () => {
      const s2 = await step(STEP2);
      const addRiskObject = await s2.getHarness(MatButtonHarness.with({ text: /Add Risk Object/ }));
      await addRiskObject.click();
      await addRiskObject.click();
      expect(component.riskObjects.length).toBe(2);

      const descriptions = await s2.getAllHarnesses(MatFormFieldHarness.with({ floatingLabelText: 'Description' }));
      await ((await descriptions[0].getControl(MatInputHarness)) as MatInputHarness).setValue('Blue sedan');

      const chips = await s2.getAllHarnesses(MatChipHarness);
      expect(await chips[0].getText()).toContain('Blue sedan');
      expect(await chips[1].getText()).toContain('New risk object');
      expect(await (await nextButton(STEP2)).isDisabled()).withContext('empty risk object description').toBeTrue();

      await chips[1].remove();
      expect(component.riskObjects.length).toBe(1);

      await (await s2.getHarness(MatButtonHarness.with({ selector: '[aria-label="Remove risk object"]' }))).click();
      expect(component.riskObjects.length).toBe(0);
    });
  });

  // ---- F-07 ----------------------------------------------------------------------------------

  describe('F-07: authority indicator', () => {
    const cases: [string, ApprovalTierName][] = [
      ['10000', 'Auto'],
      ['10000.01', 'Supervisor'],
      ['100000', 'Supervisor'],
      ['100000.01', 'Manager']
    ];

    for (const [amount, tier] of cases) {
      it(`${amount} -> ${ApprovalTierMessages[tier]}`, async () => {
        const s3 = await goToStep3();
        await (await s3.getHarness(MatSlideToggleHarness.with({ label: /Open an initial reserve/ }))).check();
        await (await control(s3, 'Amount', MatInputHarness)).setValue(amount);

        expect(text()).toContain(ApprovalTierMessages[tier]);
        for (const other of (Object.keys(ApprovalTierMessages) as ApprovalTierName[]).filter((t) => t !== tier)) {
          expect(text()).not.toContain(ApprovalTierMessages[other]);
        }
      });
    }

    it('uses the magnitude of a negative recovery amount for the tier', async () => {
      const s3 = await goToStep3();
      await (await s3.getHarness(MatSlideToggleHarness.with({ label: /Open an initial reserve/ }))).check();
      const type = await control(s3, 'Component Type', MatSelectHarness);
      await type.open();
      await type.clickOptions({ text: 'Recovery Reserve' });
      await (await control(s3, 'Amount', MatInputHarness)).setValue('-20000');
      expect(text()).toContain('Requires Supervisor approval');
    });

    it('shows no indicator until an amount is entered', async () => {
      const s3 = await goToStep3();
      await (await s3.getHarness(MatSlideToggleHarness.with({ label: /Open an initial reserve/ }))).check();
      expect(text()).not.toContain('approval');
    });
  });

  // ---- Initial reserve sign rule -------------------------------------------------------------

  describe('initial reserve sign validation', () => {
    async function openReserve(): Promise<MatStepHarness> {
      const s3 = await goToStep3();
      await (await s3.getHarness(MatSlideToggleHarness.with({ label: /Open an initial reserve/ }))).check();
      return s3;
    }

    async function amountErrors(s3: MatStepHarness): Promise<string[]> {
      return (await s3.getHarness(MatFormFieldHarness.with({ floatingLabelText: 'Amount' }))).getTextErrors();
    }

    it('rejects a positive RecoveryReserve amount and a negative Indemnity one inline', async () => {
      const s3 = await openReserve();
      const amount = await control(s3, 'Amount', MatInputHarness);
      await amount.setValue('-500');
      await amount.blur();
      expect(await amountErrors(s3)).toEqual(['Reserve amount must be greater than zero.']);

      const type = await control(s3, 'Component Type', MatSelectHarness);
      await type.open();
      await type.clickOptions({ text: 'Recovery Reserve' });
      expect(await amountErrors(s3)).withContext('-500 is a valid recovery').toEqual([]);

      await amount.setValue('500');
      expect(await amountErrors(s3)).toEqual(['Recovery reserve amounts must be negative.']);
    });

    it('blocks submit while the reserve amount has the wrong sign', async () => {
      const s3 = await openReserve();
      const type = await control(s3, 'Component Type', MatSelectHarness);
      await type.open();
      await type.clickOptions({ text: 'Recovery Reserve' });
      const amount = await control(s3, 'Amount', MatInputHarness);
      await amount.setValue('2500');
      await amount.blur();

      const button = await s3.getHarness(MatButtonHarness.with({ text: 'Submit Claim' }));
      expect(await button.isDisabled()).toBeTrue();
      http.expectNone({ method: 'POST', url: `${API}/claims` });
      expect(await amountErrors(s3)).toEqual(['Recovery reserve amounts must be negative.']);
    });

    it('keeps Submit disabled while the initial reserve is on but has no amount', async () => {
      const s3 = await openReserve();
      const amount = await control(s3, 'Amount', MatInputHarness);
      await amount.setValue('');
      await amount.blur();

      expect(await (await s3.getHarness(MatButtonHarness.with({ text: 'Submit Claim' }))).isDisabled()).toBeTrue();
      expect(await amountErrors(s3)).toEqual(['This field is required.']);

      await amount.setValue('5000');
      expect(await (await s3.getHarness(MatButtonHarness.with({ text: 'Submit Claim' }))).isDisabled()).toBeFalse();
    });

    it('submits a negative RecoveryReserve amount', async () => {
      const s3 = await openReserve();
      const type = await control(s3, 'Component Type', MatSelectHarness);
      await type.open();
      await type.clickOptions({ text: 'Recovery Reserve' });
      await (await control(s3, 'Amount', MatInputHarness)).setValue('-2500');
      await (await s3.getHarness(MatButtonHarness.with({ text: 'Submit Claim' }))).click();

      const req = http.expectOne({ method: 'POST', url: `${API}/claims` });
      expect(req.request.body.initialReserve).toEqual({ componentType: ReserveComponentType.RecoveryReserve, amount: -2500, currency: 'USD' });
      req.flush({ value: { id: 'claim-9', claimNumber: 'CLM-2026-000009' }, warnings: [] });
    });

    it('ignores the amount when no initial reserve is requested', async () => {
      await goToStep3();
      component.step3Form.patchValue({ amount: -1 });
      await (await (await step(STEP3)).getHarness(MatButtonHarness.with({ text: 'Submit Claim' }))).click();
      const req = http.expectOne({ method: 'POST', url: `${API}/claims` });
      expect(req.request.body.initialReserve).toBeNull();
      req.flush({ value: { id: 'claim-9', claimNumber: 'CLM-2026-000009' }, warnings: [] });
    });
  });

  // ---- F-08 / F-09 / F-13 --------------------------------------------------------------------

  describe('submit', () => {
    async function submit(amount = '5000'): Promise<void> {
      const s3 = await goToStep3();
      await (await s3.getHarness(MatSlideToggleHarness.with({ label: /Open an initial reserve/ }))).check();
      await (await control(s3, 'Amount', MatInputHarness)).setValue(amount);
      await (await s3.getHarness(MatButtonHarness.with({ text: 'Submit Claim' }))).click();
    }

    it('POSTs the assembled CreateClaimRequest and disables the button while pending (F-13)', async () => {
      await submit();

      const req = http.expectOne({ method: 'POST', url: `${API}/claims` });
      expect(req.request.body).toEqual(
        jasmine.objectContaining({
          policyId: null,
          assignedHandler: 'Hannah Handler',
          lossDate: new Date(2025, 0, 15).toISOString(),
          causeOfLossCodeId: 'col-1',
          parties: [jasmine.objectContaining({ name: 'Jane Claimant', partyRole: PartyRole.Claimant })],
          initialReserve: { componentType: ReserveComponentType.IndemnityReserve, amount: 5000, currency: 'USD' }
        })
      );

      const button = await (await step(STEP3)).getHarness(MatButtonHarness.with({ text: /Submitting/ }));
      expect(await button.isDisabled()).toBeTrue();

      req.flush({ value: { id: 'claim-9', claimNumber: 'CLM-2026-000009' }, warnings: [] });
    });

    it('F-09: shows a success banner with the claim number and a link to it', async () => {
      await submit();
      http.expectOne(`${API}/claims`).flush({ value: { id: 'claim-9', claimNumber: 'CLM-2026-000009' }, warnings: [] });
      fixture.detectChanges();

      expect(text()).toContain('Claim CLM-2026-000009 created.');
      const link = await loader.getHarness(MatButtonHarness.with({ text: 'View claim' }));
      expect(await (await link.host()).getAttribute('href')).toBe('/claims/claim-9');
      expect(await loader.getAllHarnesses(MatStepperHarness)).withContext('wizard is replaced by the banner').toEqual([]);
      expect(text()).not.toContain('warning');
    });

    it('F-09: shows returned warnings in the banner', async () => {
      await submit();
      http.expectOne(`${API}/claims`).flush({
        value: { id: 'claim-9', claimNumber: 'CLM-2026-000009' },
        warnings: [{ code: 'BR-C-02', message: 'Loss date falls outside the policy period.' }]
      });
      fixture.detectChanges();

      const alerts = (fixture.nativeElement as HTMLElement).querySelectorAll('[role="alert"]');
      expect(alerts.length).toBe(1);
      expect(alerts[0].textContent).toContain('Loss date falls outside the policy period.');
    });

    it('F-09: "Log another claim" resets the wizard', async () => {
      await submit();
      http.expectOne(`${API}/claims`).flush({ value: { id: 'claim-9', claimNumber: 'CLM-2026-000009' }, warnings: [] });

      await (await loader.getHarness(MatButtonHarness.with({ text: 'Log another claim' }))).click();
      expect(await selectedStepLabel()).toBe(STEP1);
      expect(component.step1Form.get('assignedHandler')!.value).toBe('');
      expect(component.parties.length).toBe(1);
    });

    it('F-08: maps 400 ProblemDetails field errors onto the matching controls and returns to step 1', async () => {
      await submit();
      http.expectOne(`${API}/claims`).flush(
        {
          type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
          title: 'One or more validation errors occurred.',
          status: 400,
          errors: {
            LossDate: ['Loss date cannot be in the future.'],
            AssignedHandler: ["'Assigned Handler' must not be empty."],
            'Parties[0].Name': ["'Name' must not be empty."],
            'InitialReserve.Amount': ['Reserve amount must be greater than zero.'],
            SomethingUnknown: ['ignored']
          }
        },
        { status: 400, statusText: 'Bad Request' }
      );
      fixture.detectChanges();

      expect(component.step1Form.get('lossDate')!.getError('server')).toBe('Loss date cannot be in the future.');
      expect(component.step1Form.get('assignedHandler')!.getError('server')).toBe("'Assigned Handler' must not be empty.");
      expect(component.parties.at(0).get('name')!.getError('server')).toBe("'Name' must not be empty.");
      expect(component.step3Form.get('amount')!.getError('server')).toBe('Reserve amount must be greater than zero.');

      expect(await selectedStepLabel()).withContext('jumps to the earliest step with an error').toBe(STEP1);
      const lossDate = await (await step(STEP1)).getHarness(MatFormFieldHarness.with({ floatingLabelText: 'Loss Date' }));
      expect(await lossDate.getTextErrors()).toEqual(['Loss date cannot be in the future.']);
      expect(await (await nextButton(STEP1)).isDisabled()).toBeTrue();
      expect(await selectedStepLabel()).toBe(STEP1);
    });

    it('F-08: a form-level "Parties" error is shown on step 2, and editing a field clears its server error', async () => {
      await submit();
      http.expectOne(`${API}/claims`).flush(
        { title: 'One or more validation errors occurred.', status: 400, errors: { Parties: ['At least one Claimant party is required.'] } },
        { status: 400, statusText: 'Bad Request' }
      );
      fixture.detectChanges();

      expect(await selectedStepLabel()).toBe(STEP2);
      expect(component.parties.getError('server')).toBe('At least one Claimant party is required.');
      expect(text()).toContain('At least one Claimant party is required.');

      const name = await control(await step(STEP2), 'Name', MatInputHarness);
      await name.setValue('Jane Q. Claimant');
      expect(component.parties.getError('server')).toBeNull();
    });

    it('keeps the wizard and re-enables Submit after a non-validation failure', async () => {
      await submit();
      http.expectOne(`${API}/claims`).flush({ title: 'An unexpected error occurred.', status: 500 }, { status: 500, statusText: 'Error' });

      expect(await loader.getAllHarnesses(MatStepperHarness)).not.toEqual([]);
      const button = await (await step(STEP3)).getHarness(MatButtonHarness.with({ text: 'Submit Claim' }));
      expect(await button.isDisabled()).toBeFalse();
    });
  });
});
