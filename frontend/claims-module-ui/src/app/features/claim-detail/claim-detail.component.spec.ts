import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ERROR_HANDLED_BY_CALLER } from '../../core/interceptors/error-handling-context';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { MatTabGroupHarness } from '@angular/material/tabs/testing';
import { MatButtonHarness } from '@angular/material/button/testing';
import { MatCardHarness } from '@angular/material/card/testing';
import { MatMenuHarness } from '@angular/material/menu/testing';
import { MatDialogHarness } from '@angular/material/dialog/testing';
import { MatInputHarness } from '@angular/material/input/testing';
import { MatFormFieldHarness } from '@angular/material/form-field/testing';
import { MatProgressSpinnerHarness } from '@angular/material/progress-spinner/testing';
import { MatSelectHarness } from '@angular/material/select/testing';

import { ClaimDetailComponent } from './claim-detail.component';
import { AuthService } from '../../core/services/auth.service';
import { SnackbarService } from '../../shared/services/snackbar.service';
import { StatusBadgeHarness } from '../../shared/components/status-badge/testing/status-badge.harness';
import { SlaBadgeHarness } from '../../shared/components/sla-badge/testing/sla-badge.harness';
import { ClaimDetailDto, ClaimDocumentDto } from '../../shared/models/claim.models';
import { ClaimStatusDto } from '../../shared/models/reference-data.models';
import { ApprovalStatus, ApprovalTier, ClaimStatus, DocumentType, ReserveComponentType } from '../../shared/models/enums';
import { API, authStub, claimDetail, emptyPage, historyEntry, reserveComponent } from '../../testing/fixtures';

type Role = 'Handler' | 'Supervisor' | 'Manager';

const STATUSES: ClaimStatusDto[] = [
  { status: ClaimStatus.Draft, allowedNextStatuses: [ClaimStatus.Open, ClaimStatus.Withdrawn] },
  { status: ClaimStatus.Open, allowedNextStatuses: [ClaimStatus.UnderInvestigation, ClaimStatus.Withdrawn] },
  { status: ClaimStatus.UnderInvestigation, allowedNextStatuses: [ClaimStatus.PendingPayment] },
  { status: ClaimStatus.Withdrawn, allowedNextStatuses: [] }
];

/** Three reserve components: a pending Supervisor-tier change, a pending Manager-tier change and an already-approved one. */
function claimWithReserves(): ClaimDetailDto {
  return claimDetail({
    reserveComponents: [
      { ...reserveComponent('res-sup', [historyEntry({ id: 'h-sup', amount: 50_000, requiredTier: ApprovalTier.Supervisor })]), componentType: ReserveComponentType.IndemnityReserve },
      { ...reserveComponent('res-mgr', [historyEntry({ id: 'h-mgr', amount: 150_000, requiredTier: ApprovalTier.Manager })]), componentType: ReserveComponentType.LitigationReserve },
      {
        ...reserveComponent('res-done', [historyEntry({ id: 'h-done', amount: 60_000, approvalStatus: ApprovalStatus.Approved, decidedBy: 'Sam Supervisor' })]),
        componentType: ReserveComponentType.ExpenseReserve
      }
    ]
  });
}

describe('ClaimDetailComponent', () => {
  let fixture: ComponentFixture<ClaimDetailComponent>;
  let loader: HarnessLoader;
  let rootLoader: HarnessLoader;
  let http: HttpTestingController;
  let snackbar: jasmine.SpyObj<SnackbarService>;

  function create(role: Role): void {
    snackbar = jasmine.createSpyObj<SnackbarService>('SnackbarService', ['success', 'warning', 'error', 'show']);
    TestBed.configureTestingModule({
      imports: [ClaimDetailComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        provideNoopAnimations(),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'claim-1' }) } } },
        { provide: AuthService, useValue: authStub(role) },
        { provide: SnackbarService, useValue: snackbar }
      ]
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ClaimDetailComponent);
    loader = TestbedHarnessEnvironment.loader(fixture);
    rootLoader = TestbedHarnessEnvironment.documentRootLoader(fixture);
    fixture.detectChanges();
  }

  /** Answers the initial page-load requests. */
  function flushLoad(claim: ClaimDetailDto, statuses = STATUSES): void {
    http.expectOne(`${API}/claims/claim-1`).flush(claim);
    http.expectOne(`${API}/reference/claim-statuses`).flush(statuses);
    http.expectOne(`${API}/claims/claim-1/documents`).flush([]);
    http.expectOne((r) => r.url === `${API}/claims/claim-1/audit`).flush(emptyPage());
    fixture.detectChanges();
  }

  /** Answers the reload the component performs after a mutation. */
  function flushReload(claim: ClaimDetailDto, statuses = STATUSES): void {
    http.expectOne(`${API}/claims/claim-1`).flush(claim);
    http.match(`${API}/reference/claim-statuses`).forEach((r) => r.flush(statuses));
    http.expectOne((r) => r.url === `${API}/claims/claim-1/audit`).flush(emptyPage());
    fixture.detectChanges();
  }

  async function openTab(label: string): Promise<void> {
    await (await loader.getHarness(MatTabGroupHarness)).selectTab({ label });
  }

  async function decisionButtons(cardTitle: string): Promise<string[]> {
    const card = await loader.getHarness(MatCardHarness.with({ title: cardTitle }));
    const buttons = await card.getAllHarnesses(MatButtonHarness.with({ selector: '[aria-label="Approve"], [aria-label="Reject"]' }));
    return Promise.all(buttons.map(async (b) => (await (await b.host()).getAttribute('aria-label'))!));
  }

  afterEach(() => http.verify());

  // ---- F-13 ----------------------------------------------------------------------------------

  describe('F-13: loading spinner', () => {
    it('shows a spinner while the claim request is pending, then the claim', async () => {
      create('Handler');
      expect((await loader.getAllHarnesses(MatProgressSpinnerHarness)).length).toBe(1);

      flushLoad(claimDetail());
      expect(await loader.getAllHarnesses(MatProgressSpinnerHarness)).toEqual([]);
      expect(fixture.nativeElement.textContent).toContain('CLM-2026-000123');
    });

    it('shows a spinner in the Audit Log tab while the audit page is loading', async () => {
      create('Handler');
      flushLoad(claimDetail());
      await openTab('Audit Log');

      fixture.componentInstance.loadAuditLog();
      fixture.detectChanges();
      expect((await loader.getAllHarnesses(MatProgressSpinnerHarness)).length).toBe(1);

      http
        .expectOne((r) => r.url === `${API}/claims/claim-1/audit`)
        .flush(emptyPage([{ id: 'a1', action: 'GL_POSTING_SIMULATED', newValues: 'DR ... CR GL-Suspense', performedBy: 'system', createdAt: '2026-09-01T10:00:00Z' }]));
      fixture.detectChanges();
      expect(await loader.getAllHarnesses(MatProgressSpinnerHarness)).toEqual([]);
      expect(fixture.nativeElement.textContent).toContain('GL_POSTING_SIMULATED');
    });
  });

  // ---- F-11 ----------------------------------------------------------------------------------

  describe('F-11: reserve approval buttons are role-gated', () => {
    it('Handler sees no Approve/Reject buttons at all', async () => {
      create('Handler');
      flushLoad(claimWithReserves());
      await openTab('Reserves');

      expect(await decisionButtons('Indemnity Reserve')).toEqual([]);
      expect(await decisionButtons('Litigation Reserve')).toEqual([]);
      expect(await decisionButtons('Expense Reserve')).toEqual([]);
    });

    it('Supervisor sees them only on Supervisor-tier pending reserves', async () => {
      create('Supervisor');
      flushLoad(claimWithReserves());
      await openTab('Reserves');

      expect(await decisionButtons('Indemnity Reserve')).toEqual(['Approve', 'Reject']);
      expect(await decisionButtons('Litigation Reserve')).withContext('Manager tier').toEqual([]);
      expect(await decisionButtons('Expense Reserve')).withContext('already approved').toEqual([]);
    });

    it('Manager sees them on every pending reserve', async () => {
      create('Manager');
      flushLoad(claimWithReserves());
      await openTab('Reserves');

      expect(await decisionButtons('Indemnity Reserve')).toEqual(['Approve', 'Reject']);
      expect(await decisionButtons('Litigation Reserve')).toEqual(['Approve', 'Reject']);
      expect(await decisionButtons('Expense Reserve')).toEqual([]);
    });

    it('hides Approve (but keeps Reject) on a change the signed-in user requested', async () => {
      create('Manager');
      const own = historyEntry({ id: 'h-own', amount: 150_000, requiredTier: ApprovalTier.Manager, requestedBy: 'Manager User' });
      flushLoad(claimDetail({ reserveComponents: [reserveComponent('res-own', [own])] }));
      await openTab('Reserves');

      expect(await decisionButtons('Indemnity Reserve')).toEqual(['Reject']);
      expect(fixture.nativeElement.textContent).toContain('Awaiting approval by another Manager');
    });

    it('disables Adjust while the reserve has a change pending approval', async () => {
      create('Handler');
      flushLoad(claimWithReserves());
      await openTab('Reserves');

      const adjust = async (title: string) =>
        (await loader.getHarness(MatCardHarness.with({ title }))).getHarness(MatButtonHarness.with({ text: /Adjust/ }));
      expect(await (await adjust('Indemnity Reserve')).isDisabled()).withContext('pending').toBeTrue();
      expect(await (await adjust('Expense Reserve')).isDisabled()).withContext('approved').toBeFalse();
    });

    it('falls back to the amount-based tier when the API omits requiredTier', async () => {
      create('Supervisor');
      const legacy = historyEntry({ id: 'h-legacy', amount: 150_000 }) as Partial<ReturnType<typeof historyEntry>>;
      delete legacy.requiredTier;
      flushLoad(claimDetail({ reserveComponents: [reserveComponent('res-legacy', [legacy as ReturnType<typeof historyEntry>])] }));
      await openTab('Reserves');

      expect(await decisionButtons('Indemnity Reserve')).toEqual([]);
    });

    it('Approve asks for confirmation, then POSTs the history id and reloads', async () => {
      create('Supervisor');
      flushLoad(claimWithReserves());
      await openTab('Reserves');

      const card = await loader.getHarness(MatCardHarness.with({ title: 'Indemnity Reserve' }));
      await (await card.getHarness(MatButtonHarness.with({ selector: '[aria-label="Approve"]' }))).click();

      const dialog = await rootLoader.getHarness(MatDialogHarness);
      expect(await dialog.getTitleText()).toBe('Approve reserve change');
      http.expectNone(`${API}/claims/claim-1/reserves/res-sup/approve`);

      await (await dialog.getHarness(MatButtonHarness.with({ text: 'Approve' }))).click();
      const approve = http.expectOne({ method: 'POST', url: `${API}/claims/claim-1/reserves/res-sup/approve` });
      expect(approve.request.body).toEqual({ reserveHistoryId: 'h-sup' });
      approve.flush({});

      flushReload(claimWithReserves());
      expect(snackbar.success).toHaveBeenCalledWith('Reserve approved.');
    });

    it('Reject requires a reason before it can be confirmed', async () => {
      create('Manager');
      flushLoad(claimWithReserves());
      await openTab('Reserves');

      const card = await loader.getHarness(MatCardHarness.with({ title: 'Litigation Reserve' }));
      await (await card.getHarness(MatButtonHarness.with({ selector: '[aria-label="Reject"]' }))).click();

      const dialog = await rootLoader.getHarness(MatDialogHarness);
      const confirm = await dialog.getHarness(MatButtonHarness.with({ text: 'Reject' }));
      expect(await confirm.isDisabled()).toBeTrue();

      await (await dialog.getHarness(MatInputHarness)).setValue('Amount not supported by the estimate');
      expect(await confirm.isDisabled()).toBeFalse();
      await confirm.click();

      const reject = http.expectOne({ method: 'POST', url: `${API}/claims/claim-1/reserves/res-mgr/reject` });
      expect(reject.request.body).toEqual({ reserveHistoryId: 'h-mgr', reason: 'Amount not supported by the estimate' });
      reject.flush({});
      flushReload(claimWithReserves());
    });
  });

  // ---- F-07 (claim detail reserve panel) -----------------------------------------------------

  describe('F-07: reserve panel tier preview', () => {
    for (const [amount, tier] of [
      ['10000', 'Auto'],
      ['10000.01', 'Supervisor'],
      ['100000.01', 'Manager']
    ]) {
      it(`${amount} -> requires ${tier} approval`, async () => {
        create('Handler');
        flushLoad(claimDetail());
        await openTab('Reserves');
        await (await loader.getHarness(MatButtonHarness.with({ text: /Open New Reserve/ }))).click();

        const amountField = await loader.getHarness(MatFormFieldHarness.with({ floatingLabelText: 'Amount' }));
        await ((await amountField.getControl(MatInputHarness)) as MatInputHarness).setValue(amount);
        expect(fixture.nativeElement.textContent).toContain(`Requires ${tier} approval`);
      });
    }

    it('submits a new reserve and surfaces returned warnings', async () => {
      create('Handler');
      flushLoad(claimDetail());
      await openTab('Reserves');
      await (await loader.getHarness(MatButtonHarness.with({ text: /Open New Reserve/ }))).click();
      const amountField = await loader.getHarness(MatFormFieldHarness.with({ floatingLabelText: 'Amount' }));
      await ((await amountField.getControl(MatInputHarness)) as MatInputHarness).setValue('50000');
      await (await loader.getHarness(MatButtonHarness.with({ text: 'Submit' }))).click();

      const open = http.expectOne({ method: 'POST', url: `${API}/claims/claim-1/reserves` });
      expect(open.request.body).toEqual({ componentType: ReserveComponentType.IndemnityReserve, amount: 50000, currency: 'USD' });
      open.flush({ value: {}, warnings: [{ code: 'W', message: 'Reserve exceeds 80% of the policy limit.' }] });
      flushReload(claimWithReserves());

      expect(snackbar.success).toHaveBeenCalledWith('Reserve submitted.');
      expect(snackbar.warning).toHaveBeenCalledWith('Reserve exceeds 80% of the policy limit.');
    });
  });

  // ---- Reserve adjust (new-total contract) ---------------------------------------------------

  describe('reserve adjust panel', () => {
    /** One approved Indemnity component at $50,000 and one approved Recovery component at -$2,000. */
    function claimWithApprovedReserves(): ClaimDetailDto {
      return claimDetail({
        reserveComponents: [
          reserveComponent('res-ind', [historyEntry({ id: 'h-ind', amount: 50_000, approvalStatus: ApprovalStatus.Approved })]),
          {
            ...reserveComponent('res-rec', [historyEntry({ id: 'h-rec', amount: -2_000, approvalStatus: ApprovalStatus.AutoApproved, requiredTier: ApprovalTier.Auto })]),
            componentType: ReserveComponentType.RecoveryReserve
          }
        ]
      });
    }

    async function openAdjust(cardTitle: string): Promise<void> {
      const card = await loader.getHarness(MatCardHarness.with({ title: cardTitle }));
      await (await card.getHarness(MatButtonHarness.with({ text: /Adjust/ }))).click();
    }

    const field = (label: RegExp) => loader.getHarness(MatFormFieldHarness.with({ floatingLabelText: label }));
    const input = async (label: RegExp) => (await (await field(label)).getControl(MatInputHarness)) as MatInputHarness;
    const submitButton = () => loader.getHarness(MatButtonHarness.with({ text: 'Submit' }));

    beforeEach(async () => {
      create('Handler');
      flushLoad(claimWithApprovedReserves());
      await openTab('Reserves');
    });

    it('labels the amount as the new total, prefilled with the current amount, and requires a reason', async () => {
      await openAdjust('Indemnity Reserve');

      expect(await (await field(/^New reserve amount/)).getLabel()).toContain('New reserve amount');
      expect(await (await input(/^New reserve amount/)).getValue()).toBe('50000');
      expect(fixture.nativeElement.textContent).toContain('current total $50,000.00');
      expect(await (await input(/^Reason for change/)).isRequired()).toBeTrue();
      expect(await (await submitButton()).isDisabled()).withContext('no reason yet, and the amount is unchanged').toBeTrue();
    });

    it('shows inline errors for a missing reason, a non-positive amount and an unchanged amount', async () => {
      await openAdjust('Indemnity Reserve');

      const reason = await input(/^Reason for change/);
      await reason.focus();
      await reason.blur();
      expect(await (await field(/^Reason for change/)).getTextErrors()).toEqual(['A reason for the change is required.']);

      const amount = await input(/^New reserve amount/);
      await amount.focus();
      await amount.blur();
      expect(await (await field(/^New reserve amount/)).getTextErrors()).toEqual(['The new amount must differ from the current amount.']);

      await amount.setValue('0');
      await amount.blur();
      expect(await (await field(/^New reserve amount/)).getTextErrors()).toEqual(['Reserve amount must be greater than zero.']);

      await amount.setValue('-100');
      expect(await (await field(/^New reserve amount/)).getTextErrors()).toEqual(['Reserve amount must be greater than zero.']);

      await reason.setValue('   ');
      expect(await (await field(/^Reason for change/)).getTextErrors()).toEqual(['A reason for the change is required.']);
      expect(await (await submitButton()).isDisabled()).toBeTrue();
      http.expectNone({ method: 'PUT', url: `${API}/claims/claim-1/reserves/res-ind` });
    });

    it('previews the authority tier on the NEW amount and PUTs the new total with the reason', async () => {
      await openAdjust('Indemnity Reserve');
      expect(fixture.nativeElement.textContent).withContext('50,000 is Supervisor tier').toContain('Requires Supervisor approval');

      // A 150,000 new total is a +100,000 change, but the tier follows the new total: Manager.
      await (await input(/^New reserve amount/)).setValue('150000');
      expect(fixture.nativeElement.textContent).toContain('Requires Manager approval');
      await (await input(/^Reason for change/)).setValue('Repair estimate increased');

      const submit = await submitButton();
      expect(await submit.isDisabled()).toBeFalse();
      await submit.click();

      const put = http.expectOne({ method: 'PUT', url: `${API}/claims/claim-1/reserves/res-ind` });
      expect(put.request.body).toEqual({ amount: 150000, reason: 'Repair estimate increased', currency: 'USD' });
      put.flush({ value: {}, warnings: [] });
      flushReload(claimWithApprovedReserves());
      expect(snackbar.success).toHaveBeenCalledWith('Reserve submitted.');
    });

    it('requires a negative new total for a RecoveryReserve', async () => {
      await openAdjust('Recovery Reserve');
      const amount = await input(/^New reserve amount/);
      expect(await amount.getValue()).toBe('-2000');

      await amount.setValue('500');
      await amount.blur();
      expect(await (await field(/^New reserve amount/)).getTextErrors()).toEqual(['Recovery reserve amounts must be negative.']);

      await amount.setValue('-3500');
      expect(await (await field(/^New reserve amount/)).getTextErrors()).toEqual([]);
      await (await input(/^Reason for change/)).setValue('Salvage value revised');
      await (await submitButton()).click();

      const put = http.expectOne({ method: 'PUT', url: `${API}/claims/claim-1/reserves/res-rec` });
      expect(put.request.body).toEqual({ amount: -3500, reason: 'Salvage value revised', currency: 'USD' });
      put.flush({ value: {}, warnings: [] });
      flushReload(claimWithApprovedReserves());
    });

    it('maps 400 errors keyed by Amount / Reason onto the inline errors', async () => {
      await openAdjust('Indemnity Reserve');
      await (await input(/^New reserve amount/)).setValue('60000');
      await (await input(/^Reason for change/)).setValue('Update');
      await (await submitButton()).click();

      http.expectOne({ method: 'PUT', url: `${API}/claims/claim-1/reserves/res-ind` }).flush(
        { title: 'One or more validation errors occurred.', status: 400, errors: { Reason: ['Reason is too vague.'], Amount: ['Amount exceeds the policy limit.'] } },
        { status: 400, statusText: 'Bad Request' }
      );
      fixture.detectChanges();

      expect(await (await field(/^Reason for change/)).getTextErrors()).toEqual(['Reason is too vague.']);
      expect(await (await field(/^New reserve amount/)).getTextErrors()).toEqual(['Amount exceeds the policy limit.']);
    });
  });

  describe('open reserve panel', () => {
    beforeEach(async () => {
      create('Handler');
      flushLoad(claimDetail());
      await openTab('Reserves');
      await (await loader.getHarness(MatButtonHarness.with({ text: /Open New Reserve/ }))).click();
    });

    const field = (label: RegExp) => loader.getHarness(MatFormFieldHarness.with({ floatingLabelText: label }));
    const input = async (label: RegExp) => (await (await field(label)).getControl(MatInputHarness)) as MatInputHarness;

    it('has an optional reason that is sent when filled in', async () => {
      expect(await (await input(/^Reason \(optional\)/)).isRequired()).toBeFalse();
      await (await input(/^Amount/)).setValue('7500');
      await (await input(/^Reason \(optional\)/)).setValue('Initial estimate from adjuster');
      await (await loader.getHarness(MatButtonHarness.with({ text: 'Submit' }))).click();

      const open = http.expectOne({ method: 'POST', url: `${API}/claims/claim-1/reserves` });
      expect(open.request.body).toEqual({
        componentType: ReserveComponentType.IndemnityReserve,
        amount: 7500,
        currency: 'USD',
        reason: 'Initial estimate from adjuster'
      });
      open.flush({ value: {}, warnings: [] });
      flushReload(claimDetail());
    });

    it('validates the sign against the selected component type', async () => {
      const amount = await input(/^Amount/);
      await amount.setValue('-10');
      await amount.blur();
      expect(await (await field(/^Amount/)).getTextErrors()).toEqual(['Reserve amount must be greater than zero.']);

      const type = (await (await field(/^Component Type/)).getControl(MatSelectHarness)) as MatSelectHarness;
      await type.open();
      await type.clickOptions({ text: 'Recovery Reserve' });
      expect(await (await field(/^Amount/)).getTextErrors()).withContext('-10 is valid for a recovery').toEqual([]);

      await amount.setValue('2500');
      expect(await (await field(/^Amount/)).getTextErrors()).toEqual(['Recovery reserve amounts must be negative.']);
      expect(await (await loader.getHarness(MatButtonHarness.with({ text: 'Submit' }))).isDisabled()).toBeTrue();

      await amount.setValue('-2500');
      await (await loader.getHarness(MatButtonHarness.with({ text: 'Submit' }))).click();
      const open = http.expectOne({ method: 'POST', url: `${API}/claims/claim-1/reserves` });
      expect(open.request.body).toEqual({ componentType: ReserveComponentType.RecoveryReserve, amount: -2500, currency: 'USD' });
      open.flush({ value: {}, warnings: [] });
      flushReload(claimDetail());
    });
  });

  // ---- Reserve history -----------------------------------------------------------------------

  describe('reserve history table', () => {
    it('shows Previous → New, the signed change, the reason and who changed it', async () => {
      create('Handler');
      flushLoad(
        claimDetail({
          reserveComponents: [
            reserveComponent('res-1', [
              historyEntry({ id: 'h1', changeSequence: 1, amount: 50_000, approvalStatus: ApprovalStatus.Approved, requestedBy: 'Hannah Handler' }),
              historyEntry({
                id: 'h2',
                changeSequence: 2,
                previousAmount: 50_000,
                newAmount: 40_000,
                changeReason: 'Salvage recovered',
                requiredTier: ApprovalTier.Supervisor,
                requestedBy: 'Harry Handler'
              })
            ])
          ]
        })
      );
      await openTab('Reserves');

      const table = (fixture.nativeElement as HTMLElement).querySelector('mat-card.reserve-card table')!;
      const headers = Array.from(table.querySelectorAll('thead th')).map((th) => th.textContent!.trim());
      expect(headers).toEqual(['#', 'Previous → New', 'Change', 'Reason', 'Status', 'GL Posting', 'Changed By', 'Decided By', '']);

      const rows = Array.from(table.querySelectorAll('tbody tr')).map((tr) =>
        Array.from(tr.querySelectorAll('td')).map((td) => td.textContent!.replace(/\s+/g, ' ').trim())
      );
      // Newest change first.
      expect(rows[0].slice(0, 4)).toEqual(['2', '$50,000.00 → $40,000.00', '-$10,000.00', 'Salvage recovered']);
      expect(rows[0][6]).toBe('Harry Handler');
      expect(rows[1].slice(0, 4)).toEqual(['1', '$0.00 → $50,000.00', '+$50,000.00', 'Initial reserve']);
      expect(rows[1][6]).toBe('Hannah Handler');

      const changeCells = table.querySelectorAll('td.history-change');
      expect(changeCells[0].classList).toContain('history-change--negative');
      expect(changeCells[1].classList).not.toContain('history-change--negative');
    });
  });

  // ---- BR-R-07 manager override --------------------------------------------------------------

  describe('manager override on opening a reserve (BR-R-07)', () => {
    const CAP_TITLE = 'Aggregate reserve exceeds $10,000,000; a manager override is required to proceed.';

    async function openReserveViaUi(amount: string): Promise<void> {
      await openTab('Reserves');
      await (await loader.getHarness(MatButtonHarness.with({ text: /Open New Reserve/ }))).click();
      const amountField = await loader.getHarness(MatFormFieldHarness.with({ floatingLabelText: 'Amount' }));
      await ((await amountField.getControl(MatInputHarness)) as MatInputHarness).setValue(amount);
      await (await loader.getHarness(MatButtonHarness.with({ text: 'Submit' }))).click();
    }

    it('asks a Manager to confirm the override after a cap 422, then resubmits with managerOverrideConfirmed', async () => {
      create('Manager');
      flushLoad(claimDetail());
      await openReserveViaUi('5000');

      const first = http.expectOne({ method: 'POST', url: `${API}/claims/claim-1/reserves` });
      expect(first.request.body.managerOverrideConfirmed).toBeUndefined();
      expect(first.request.context.get(ERROR_HANDLED_BY_CALLER)(new HttpErrorResponse({ status: 422, error: { title: CAP_TITLE } }))).toBeTrue();
      first.flush({ title: CAP_TITLE, status: 422 }, { status: 422, statusText: 'Unprocessable Entity' });

      const dialog = await rootLoader.getHarness(MatDialogHarness);
      expect(await dialog.getTitleText()).toBe('Manager override required');
      await (await dialog.getHarness(MatButtonHarness.with({ text: 'Submit with override' }))).click();

      const retry = http.expectOne({ method: 'POST', url: `${API}/claims/claim-1/reserves` });
      expect(retry.request.body).toEqual({ componentType: ReserveComponentType.IndemnityReserve, amount: 5000, currency: 'USD', managerOverrideConfirmed: true });
      retry.flush({ value: {}, warnings: [] });
      flushReload(claimDetail());
      expect(snackbar.success).toHaveBeenCalledWith('Reserve submitted with manager override.');
    });

    it('offers no override to a Handler — the cap error is left to the global snackbar', async () => {
      create('Handler');
      flushLoad(claimDetail());
      await openReserveViaUi('5000');

      const req = http.expectOne({ method: 'POST', url: `${API}/claims/claim-1/reserves` });
      expect(req.request.context.get(ERROR_HANDLED_BY_CALLER)(new HttpErrorResponse({ status: 422, error: { title: CAP_TITLE } }))).toBeFalse();
      req.flush({ title: CAP_TITLE, status: 422 }, { status: 422, statusText: 'Unprocessable Entity' });

      expect(await rootLoader.getAllHarnesses(MatDialogHarness)).toEqual([]);
    });
  });

  describe('manager override on approve (BR-R-07)', () => {
    const CAP_TITLE = "Approving this change takes the claim's aggregate reserve over $10,000,000; a manager override is required.";
    const capError = () => new HttpErrorResponse({ status: 422, error: { title: CAP_TITLE } });

    async function approveViaUi(cardTitle: string): Promise<void> {
      const card = await loader.getHarness(MatCardHarness.with({ title: cardTitle }));
      await (await card.getHarness(MatButtonHarness.with({ selector: '[aria-label="Approve"]' }))).click();
      const dialog = await rootLoader.getHarness(MatDialogHarness);
      await (await dialog.getHarness(MatButtonHarness.with({ text: 'Approve' }))).click();
    }

    it('asks a Manager to confirm the override after a cap 422, then retries with managerOverrideConfirmed', async () => {
      create('Manager');
      flushLoad(claimWithReserves());
      await openTab('Reserves');
      await approveViaUi('Litigation Reserve');

      const first = http.expectOne({ method: 'POST', url: `${API}/claims/claim-1/reserves/res-mgr/approve` });
      expect(first.request.body).toEqual({ reserveHistoryId: 'h-mgr' });
      // The component owns this error (it offers the override), so the global snackbar must stay quiet for it.
      expect(first.request.context.get(ERROR_HANDLED_BY_CALLER)(capError())).toBeTrue();
      first.flush({ type: 'BusinessRuleViolation', title: CAP_TITLE, status: 422 }, { status: 422, statusText: 'Unprocessable Entity' });

      const dialog = await rootLoader.getHarness(MatDialogHarness);
      expect(await dialog.getTitleText()).toBe('Manager override required');
      expect(await dialog.getContentText()).toContain('Approving takes the claim over $10M — approve with manager override?');
      await (await dialog.getHarness(MatButtonHarness.with({ text: 'Approve with override' }))).click();

      const retry = http.expectOne({ method: 'POST', url: `${API}/claims/claim-1/reserves/res-mgr/approve` });
      expect(retry.request.body).toEqual({ reserveHistoryId: 'h-mgr', managerOverrideConfirmed: true });
      retry.flush({});
      flushReload(claimWithReserves());
      expect(snackbar.success).toHaveBeenCalledWith('Reserve approved with manager override.');
    });

    it('does not retry when the Manager cancels the override', async () => {
      create('Manager');
      flushLoad(claimWithReserves());
      await openTab('Reserves');
      await approveViaUi('Litigation Reserve');
      http
        .expectOne({ method: 'POST', url: `${API}/claims/claim-1/reserves/res-mgr/approve` })
        .flush({ title: CAP_TITLE, status: 422 }, { status: 422, statusText: 'Unprocessable Entity' });

      const dialog = await rootLoader.getHarness(MatDialogHarness);
      await (await dialog.getHarness(MatButtonHarness.with({ text: 'Cancel' }))).click();
      http.expectNone({ method: 'POST', url: `${API}/claims/claim-1/reserves/res-mgr/approve` });
      expect(snackbar.success).not.toHaveBeenCalled();
    });

    it('offers no override to a Supervisor', async () => {
      create('Supervisor');
      flushLoad(claimWithReserves());
      await openTab('Reserves');
      await approveViaUi('Indemnity Reserve');
      const request = http.expectOne({ method: 'POST', url: `${API}/claims/claim-1/reserves/res-sup/approve` });
      // No override on offer, so the cap error is left to the global snackbar.
      expect(request.request.context.get(ERROR_HANDLED_BY_CALLER)(capError())).toBeFalse();
      request.flush({ title: CAP_TITLE, status: 422 }, { status: 422, statusText: 'Unprocessable Entity' });
      expect(await rootLoader.getAllHarnesses(MatDialogHarness)).toEqual([]);
    });

    it('offers no override for an unrelated 422', async () => {
      create('Manager');
      flushLoad(claimWithReserves());
      await openTab('Reserves');
      await approveViaUi('Litigation Reserve');
      http
        .expectOne({ method: 'POST', url: `${API}/claims/claim-1/reserves/res-mgr/approve` })
        .flush({ title: 'Reserve change is not pending approval.', status: 422 }, { status: 422, statusText: 'Unprocessable Entity' });
      expect(await rootLoader.getAllHarnesses(MatDialogHarness)).toEqual([]);
    });
  });

  // ---- F-12 ----------------------------------------------------------------------------------

  describe('F-12: status transitions', () => {
    it('offers only the allowed next statuses for the current status', async () => {
      create('Handler');
      flushLoad(claimDetail({ status: ClaimStatus.Open }));

      const menu = await loader.getHarness(MatMenuHarness.with({ triggerText: /Transition Status/ }));
      await menu.open();
      const items = await Promise.all((await menu.getItems()).map((i) => i.getText()));
      expect(items).toEqual(['Under Investigation', 'Withdrawn']);
    });

    it('hides the button when no transitions are allowed', async () => {
      create('Handler');
      flushLoad(claimDetail({ status: ClaimStatus.Withdrawn }));
      expect(await loader.getAllHarnesses(MatMenuHarness)).toEqual([]);
    });

    it('does nothing when the confirmation dialog is cancelled', async () => {
      create('Handler');
      flushLoad(claimDetail({ status: ClaimStatus.Open }));

      const menu = await loader.getHarness(MatMenuHarness.with({ triggerText: /Transition Status/ }));
      await menu.clickItem({ text: 'Withdrawn' });
      const dialog = await rootLoader.getHarness(MatDialogHarness);
      expect(await dialog.getContentText()).toContain('Transition this claim from Open to Withdrawn?');
      await (await dialog.getHarness(MatButtonHarness.with({ text: 'Cancel' }))).click();

      http.expectNone({ method: 'PUT', url: `${API}/claims/claim-1/status` });
      expect(await rootLoader.getAllHarnesses(MatDialogHarness)).toEqual([]);
    });

    it('confirms, PUTs the new status, then updates the chip and the available transitions', async () => {
      create('Handler');
      flushLoad(claimDetail({ status: ClaimStatus.Open }));
      expect(await (await loader.getHarness(StatusBadgeHarness.with({ label: 'Open' }))).getColor()).toBe('info');

      const menu = await loader.getHarness(MatMenuHarness.with({ triggerText: /Transition Status/ }));
      await menu.clickItem({ text: 'Under Investigation' });
      const dialog = await rootLoader.getHarness(MatDialogHarness);
      await (await dialog.getHarness(MatButtonHarness.with({ text: 'Transition' }))).click();

      const put = http.expectOne({ method: 'PUT', url: `${API}/claims/claim-1/status` });
      expect(put.request.body).toEqual({ newStatus: ClaimStatus.UnderInvestigation });
      put.flush(claimDetail({ status: ClaimStatus.UnderInvestigation }));
      http.expectOne(`${API}/reference/claim-statuses`).flush(STATUSES);
      http.expectOne((r) => r.url === `${API}/claims/claim-1/audit`).flush(emptyPage());
      fixture.detectChanges();

      const badge = await loader.getHarness(StatusBadgeHarness.with({ label: 'Under Investigation' }));
      expect(await badge.getColor()).toBe('orange');
      expect(snackbar.success).toHaveBeenCalledWith('Claim status updated to Under Investigation.');

      await menu.open();
      expect(await Promise.all((await menu.getItems()).map((i) => i.getText()))).toEqual(['Pending Payment']);
    });
  });

  // ---- Documents -----------------------------------------------------------------------------

  it('links each document to its resolved download URL', async () => {
    create('Handler');
    http.expectOne(`${API}/claims/claim-1`).flush(claimDetail());
    http.expectOne(`${API}/reference/claim-statuses`).flush(STATUSES);
    http.expectOne(`${API}/claims/claim-1/documents`).flush([
      { id: 'd1', fileName: 'report.pdf', contentType: 'application/pdf', sizeBytes: 2048, documentType: DocumentType.PoliceReport, uploadedBy: 'H', createdAt: '2026-09-01T10:00:00Z', downloadUrl: '/api/documents/d1/content' },
      { id: 'd2', fileName: 'photo.jpg', contentType: 'image/jpeg', sizeBytes: 10, documentType: DocumentType.Photo, uploadedBy: 'H', createdAt: '2026-09-01T10:00:00Z', downloadUrl: 'https://blob.example/x?sig=1' }
    ]);
    http.expectOne((r) => r.url === `${API}/claims/claim-1/audit`).flush(emptyPage());
    fixture.detectChanges();
    await openTab('Documents');

    const pdf = await loader.getHarness(MatButtonHarness.with({ selector: '[aria-label="Download report.pdf"]' }));
    expect(await (await pdf.host()).getAttribute('href')).toBe('http://localhost:5299/api/documents/d1/content');
    const photo = await loader.getHarness(MatButtonHarness.with({ selector: '[aria-label="Download photo.jpg"]' }));
    expect(await (await photo.host()).getAttribute('href')).toBe('https://blob.example/x?sig=1');
    expect(fixture.nativeElement.textContent).toContain('2.0 KB');
    expect(fixture.nativeElement.textContent).withContext('document type column').toContain('Police Report');
  });

  describe('Documents: document type on upload', () => {
    const uploaded: ClaimDocumentDto = {
      id: 'd9',
      fileName: 'police.pdf',
      contentType: 'application/pdf',
      sizeBytes: 100,
      documentType: DocumentType.PoliceReport,
      uploadedBy: 'Hannah Handler',
      createdAt: '2026-09-01T10:00:00Z',
      downloadUrl: '/api/documents/d9/content'
    };

    function chooseFile(name: string): void {
      const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('input[type="file"]')!;
      const transfer = new DataTransfer();
      transfer.items.add(new File(['%PDF-1.4'], name, { type: 'application/pdf' }));
      input.files = transfer.files;
      input.dispatchEvent(new Event('change'));
    }

    async function typeSelect(): Promise<MatSelectHarness> {
      const formField = await loader.getHarness(MatFormFieldHarness.with({ floatingLabelText: 'Document type' }));
      return (await formField.getControl(MatSelectHarness)) as MatSelectHarness;
    }

    it('defaults the type select to Other and sends it as the documentType form field', async () => {
      create('Handler');
      flushLoad(claimDetail());
      await openTab('Documents');
      expect(await (await typeSelect()).getValueText()).toBe('Other');

      chooseFile('misc.pdf');
      const req = http.expectOne({ method: 'POST', url: `${API}/claims/claim-1/documents` });
      expect((req.request.body as FormData).get('documentType')).toBe(String(DocumentType.Other));
      req.flush({ ...uploaded, documentType: DocumentType.Other });
      http.expectOne(`${API}/claims/claim-1/documents`).flush([]);
      http.expectOne((r) => r.url === `${API}/claims/claim-1/audit`).flush(emptyPage());
    });

    it('sends the chosen type and lists it after the upload', async () => {
      create('Handler');
      flushLoad(claimDetail());
      await openTab('Documents');

      const select = await typeSelect();
      await select.open();
      await select.clickOptions({ text: 'Police Report' });

      chooseFile('police.pdf');
      const req = http.expectOne({ method: 'POST', url: `${API}/claims/claim-1/documents` });
      const body = req.request.body as FormData;
      expect(body.get('documentType')).toBe(String(DocumentType.PoliceReport));
      expect((body.get('file') as File).name).toBe('police.pdf');
      req.flush(uploaded);

      http.expectOne(`${API}/claims/claim-1/documents`).flush([uploaded]);
      http.expectOne((r) => r.url === `${API}/claims/claim-1/audit`).flush(emptyPage());
      fixture.detectChanges();

      const row = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr')).find((tr) => tr.textContent?.includes('police.pdf'))!;
      expect(row.textContent).toContain('Police Report');
      expect(snackbar.success).toHaveBeenCalledWith('Document uploaded.');
    });
  });

  // ---- SLA flag ------------------------------------------------------------------------------

  describe('SLA breached chip', () => {
    it('is shown next to the status badge with the breach time in its tooltip', async () => {
      create('Handler');
      flushLoad(claimDetail({ isSlaBreached: true, slaBreachedAt: '2026-09-20T08:30:00Z' }));

      const header = (fixture.nativeElement as HTMLElement).querySelector('.claim-header__main')!;
      expect(header.querySelector('app-status-badge + app-sla-badge')).withContext('directly after the status badge').not.toBeNull();

      const chip = await loader.getHarness(SlaBadgeHarness);
      expect(await chip.getText()).toContain('SLA breached');
      const tooltip = await chip.getTooltipText();
      expect(tooltip).toContain('SLA breached since');
      expect(tooltip).toContain('2026');
    });

    it('is absent when the claim is within SLA', async () => {
      create('Handler');
      flushLoad(claimDetail());
      expect(await loader.getAllHarnesses(SlaBadgeHarness)).toEqual([]);
    });
  });
});
