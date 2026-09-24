import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { HarnessLoader } from '@angular/cdk/testing';
import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { MatProgressSpinnerHarness } from '@angular/material/progress-spinner/testing';
import { MatTableHarness } from '@angular/material/table/testing';
import { MatPaginatorHarness } from '@angular/material/paginator/testing';
import { MatSelectHarness } from '@angular/material/select/testing';
import { MatFormFieldHarness } from '@angular/material/form-field/testing';
import { MatInputHarness } from '@angular/material/input/testing';
import { MatButtonHarness } from '@angular/material/button/testing';

import { ClaimsListComponent } from './claims-list.component';
import { StatusBadgeHarness } from '../../shared/components/status-badge/testing/status-badge.harness';
import { SlaBadgeHarness } from '../../shared/components/sla-badge/testing/sla-badge.harness';
import { ClaimListItemDto } from '../../shared/models/claim.models';
import { ClaimStatus } from '../../shared/models/enums';
import { API } from '../../testing/fixtures';

function claimRow(n: number, status = ClaimStatus.Open): ClaimListItemDto {
  return {
    id: `claim-${n}`,
    claimNumber: `CLM-2026-${String(n).padStart(6, '0')}`,
    policyNumber: 'POL-2026-000001',
    clientName: 'Acme Corp',
    lossDate: '2026-09-01T00:00:00Z',
    causeOfLossCode: 'COLL',
    status,
    assignedHandler: 'Hannah Handler',
    totalReserve: 5000,
    currency: 'USD',
    isSlaBreached: false,
    slaBreachedAt: null
  };
}

describe('ClaimsListComponent', () => {
  let fixture: ComponentFixture<ClaimsListComponent>;
  let loader: HarnessLoader;
  let http: HttpTestingController;

  const listRequest = (): TestRequest => http.expectOne((r) => r.url === `${API}/claims`);
  const page = (items: ClaimListItemDto[], totalCount = items.length, pageNumber = 1, pageSize = 20) => ({
    items,
    pageNumber,
    pageSize,
    totalCount,
    totalPages: Math.ceil(totalCount / pageSize)
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ClaimsListComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), provideNoopAnimations()]
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ClaimsListComponent);
    loader = TestbedHarnessEnvironment.loader(fixture);
    fixture.detectChanges();
    http.expectOne(`${API}/reference/cause-of-loss-codes`).flush([{ id: 'col-1', code: 'COLL', description: 'Collision', perilCategory: 'Auto' }]);
  });

  afterEach(() => http.verify());

  it('F-13: shows a spinner while the list request is pending, then the table', async () => {
    expect((await loader.getAllHarnesses(MatProgressSpinnerHarness)).length).toBe(1);

    listRequest().flush(page([claimRow(1), claimRow(2, ClaimStatus.Reopened)]));
    fixture.detectChanges();

    expect(await loader.getAllHarnesses(MatProgressSpinnerHarness)).toEqual([]);
    const table = await loader.getHarness(MatTableHarness);
    const rows = await table.getCellTextByColumnName();
    expect(rows['claimNumber'].text).toEqual(['CLM-2026-000001', 'CLM-2026-000002']);
    expect(await (await loader.getHarness(StatusBadgeHarness.with({ label: 'Reopened' }))).getColor()).toBe('teal');
  });

  it('shows an "SLA breached" chip beside the status badge only on breached rows', async () => {
    const breached = { ...claimRow(2), isSlaBreached: true, slaBreachedAt: '2026-09-20T08:30:00Z' };
    listRequest().flush(page([claimRow(1), breached]));
    fixture.detectChanges();

    const table = await loader.getHarness(MatTableHarness);
    const [plain, flagged] = await table.getRows();
    const [plainStatus] = await plain.getCells({ columnName: 'status' });
    const [flaggedStatus] = await flagged.getCells({ columnName: 'status' });

    expect(await plainStatus.getAllHarnesses(SlaBadgeHarness)).toEqual([]);
    const [chip] = await flaggedStatus.getAllHarnesses(SlaBadgeHarness);
    expect(await chip.getText()).toContain('SLA breached');
    expect(await chip.getTooltipText()).toContain('SLA breached since');
    expect(await (await flaggedStatus.getHarness(StatusBadgeHarness)).getLabel()).toBe('Open');
  });

  it('shows the empty state when nothing matches', () => {
    listRequest().flush(page([]));
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('No claims match these filters.');
  });

  it('F-03: paging sends pageNumber/pageSize', async () => {
    listRequest().flush(page([claimRow(1)], 45));
    fixture.detectChanges();

    const paginator = await loader.getHarness(MatPaginatorHarness);
    await paginator.goToNextPage();
    const next = listRequest();
    expect(next.request.params.get('pageNumber')).toBe('2');
    expect(next.request.params.get('pageSize')).toBe('20');
    next.flush(page([claimRow(21)], 45, 2));
    fixture.detectChanges();

    await (await loader.getHarness(MatPaginatorHarness)).setPageSize(50);
    const resized = listRequest();
    expect(resized.request.params.get('pageSize')).toBe('50');
    resized.flush(page([claimRow(1)], 45, 1, 50));
  });

  it('lists the status filter options in alphabetical order', async () => {
    listRequest().flush(page([claimRow(1)]));
    fixture.detectChanges();

    const status = (await (await loader.getHarness(MatFormFieldHarness.with({ floatingLabelText: 'Status' }))).getControl(MatSelectHarness)) as MatSelectHarness;
    await status.open();
    const labels = await Promise.all((await status.getOptions()).map((o) => o.getText()));
    expect(labels).toEqual(['Closed', 'Draft', 'Open', 'Pending Payment', 'Reopened', 'Under Investigation', 'Withdrawn']);
    await status.close();
  });

  it('F-03: status filter refilters on every selection, sends repeated statuses and resets to page 1', async () => {
    listRequest().flush(page([claimRow(1)]));
    fixture.detectChanges();

    const status = (await (await loader.getHarness(MatFormFieldHarness.with({ floatingLabelText: 'Status' }))).getControl(MatSelectHarness)) as MatSelectHarness;
    await status.open();
    await status.clickOptions({ text: 'Open' });
    await status.clickOptions({ text: 'Reopened' });

    // One request per selection, while the panel is still open; the superseded one is cancelled.
    const [first, latest] = http.match((r) => r.url === `${API}/claims`);
    expect(first.request.params.getAll('statuses')).toEqual([String(ClaimStatus.Open)]);
    expect(first.cancelled).toBeTrue();
    expect(latest.request.params.getAll('statuses')).toEqual([String(ClaimStatus.Open), String(ClaimStatus.Reopened)]);
    expect(latest.request.params.get('pageNumber')).toBe('1');
    latest.flush(page([claimRow(1)]));

    await status.close();
  });

  it('F-03: handler filter is debounced into a single request', async () => {
    listRequest().flush(page([claimRow(1)]));
    fixture.detectChanges();

    const handler = (await (await loader.getHarness(MatFormFieldHarness.with({ floatingLabelText: 'Assigned handler' }))).getControl(MatInputHarness)) as MatInputHarness;
    // sendKeys (not setValue) so the harness doesn't clear the field first, which would stabilise and fire its own request.
    await (await handler.host()).sendKeys('Hannah');

    const requests = http.match((r) => r.url === `${API}/claims`);
    expect(requests.length).toBe(1);
    expect(requests[0].request.params.get('assignedHandler')).toBe('Hannah');
    requests[0].flush(page([]));
  });

  it('F-03: cause-of-loss filter and Clear', async () => {
    listRequest().flush(page([claimRow(1)]));
    fixture.detectChanges();

    const cause = (await (await loader.getHarness(MatFormFieldHarness.with({ floatingLabelText: 'Cause of loss' }))).getControl(MatSelectHarness)) as MatSelectHarness;
    await cause.open();
    await cause.clickOptions({ text: 'Collision' });
    const filtered = listRequest();
    expect(filtered.request.params.get('causeOfLossCodeId')).toBe('col-1');
    filtered.flush(page([]));
    fixture.detectChanges();

    await (await loader.getHarness(MatButtonHarness.with({ text: 'Clear' }))).click();
    const cleared = listRequest();
    expect(cleared.request.params.keys().sort()).toEqual(['pageNumber', 'pageSize']);
    cleared.flush(page([]));
  });

  it('navigates to the claim when a row is clicked, and to FNOL from the header button', async () => {
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    listRequest().flush(page([claimRow(7)]));
    fixture.detectChanges();

    const [row] = await (await loader.getHarness(MatTableHarness)).getRows();
    await (await row.host()).click();
    expect(navigate).toHaveBeenCalledWith(['/claims', 'claim-7']);

    await (await loader.getHarness(MatButtonHarness.with({ text: /Log New Claim/ }))).click();
    expect(navigate).toHaveBeenCalledWith(['/claims/new']);
  });
});
