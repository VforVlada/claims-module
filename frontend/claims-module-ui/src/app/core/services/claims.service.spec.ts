import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ClaimsService } from './claims.service';
import { ClaimStatus, ClaimType, DocumentType, PartyRole, PartyType } from '../../shared/models/enums';
import { API } from '../../testing/fixtures';

// F-03: API services build the correct URLs and query params for filters and paging.
describe('ClaimsService (F-03)', () => {
  let service: ClaimsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ClaimsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  describe('list()', () => {
    it('sends only paging params when no filters are set', () => {
      service.list({ pageNumber: 1, pageSize: 20 }).subscribe();

      const req = http.expectOne((r) => r.url === `${API}/claims`);
      expect(req.request.method).toBe('GET');
      expect(req.request.params.keys().sort()).toEqual(['pageNumber', 'pageSize']);
      expect(req.request.params.get('pageNumber')).toBe('1');
      expect(req.request.params.get('pageSize')).toBe('20');
      req.flush({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 });
    });

    it('repeats statuses and includes every other filter', () => {
      service
        .list({
          statuses: [ClaimStatus.Open, ClaimStatus.Reopened],
          fromDate: '2026-01-01T00:00:00.000Z',
          toDate: '2026-02-01T00:00:00.000Z',
          assignedHandler: 'Hannah',
          causeOfLossCodeId: 'col-9',
          pageNumber: 3,
          pageSize: 50
        })
        .subscribe();

      const req = http.expectOne((r) => r.url === `${API}/claims`);
      const params = req.request.params;
      expect(params.getAll('statuses')).toEqual(['1', '5']);
      expect(params.get('fromDate')).toBe('2026-01-01T00:00:00.000Z');
      expect(params.get('toDate')).toBe('2026-02-01T00:00:00.000Z');
      expect(params.get('assignedHandler')).toBe('Hannah');
      expect(params.get('causeOfLossCodeId')).toBe('col-9');
      expect(params.get('pageNumber')).toBe('3');
      expect(params.get('pageSize')).toBe('50');
      expect(req.request.urlWithParams).toContain('statuses=1&statuses=5');
      req.flush({ items: [], pageNumber: 3, pageSize: 50, totalCount: 0, totalPages: 0 });
    });

    it('omits empty optional filters', () => {
      service.list({ statuses: [], assignedHandler: '', causeOfLossCodeId: '', pageNumber: 1, pageSize: 10 }).subscribe();

      const req = http.expectOne((r) => r.url === `${API}/claims`);
      expect(req.request.params.has('statuses')).toBeFalse();
      expect(req.request.params.has('assignedHandler')).toBeFalse();
      expect(req.request.params.has('causeOfLossCodeId')).toBeFalse();
      req.flush({ items: [], pageNumber: 1, pageSize: 10, totalCount: 0, totalPages: 0 });
    });
  });

  it('getAuditLog() pages via pageNumber/pageSize (defaults 1/20)', () => {
    service.getAuditLog('c1').subscribe();
    service.getAuditLog('c1', 2, 50).subscribe();

    const [first, second] = http.match((r) => r.url === `${API}/claims/c1/audit`);
    expect(first.request.urlWithParams).toBe(`${API}/claims/c1/audit?pageNumber=1&pageSize=20`);
    expect(second.request.urlWithParams).toBe(`${API}/claims/c1/audit?pageNumber=2&pageSize=50`);
    first.flush({ items: [] });
    second.flush({ items: [] });
  });

  it('create() POSTs the request body to /claims', () => {
    const body = {
      claimType: ClaimType.Auto,
      lossDate: '2026-09-01T00:00:00.000Z',
      lossDescription: 'd',
      lossLocation: 'l',
      causeOfLossCodeId: 'col-1',
      assignedHandler: 'h',
      parties: [{ partyType: PartyType.Individual, partyRole: PartyRole.Claimant, name: 'Jane' }],
      riskObjects: []
    };
    service.create(body).subscribe();

    const req = http.expectOne(`${API}/claims`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({ value: {}, warnings: [] });
  });

  it('getById(), getDocuments(), transitionStatus() and addParty() hit the claim-scoped URLs', () => {
    service.getById('c1').subscribe();
    service.getDocuments('c1').subscribe();
    service.transitionStatus('c1', ClaimStatus.Closed).subscribe();
    service.addParty('c1', { partyType: PartyType.Organization, partyRole: PartyRole.Witness, name: 'W' }).subscribe();

    expect(http.expectOne({ method: 'GET', url: `${API}/claims/c1` })).toBeTruthy();
    expect(http.expectOne({ method: 'GET', url: `${API}/claims/c1/documents` })).toBeTruthy();
    const transition = http.expectOne({ method: 'PUT', url: `${API}/claims/c1/status` });
    expect(transition.request.body).toEqual({ newStatus: ClaimStatus.Closed });
    const party = http.expectOne({ method: 'POST', url: `${API}/claims/c1/parties` });
    expect(party.request.body.name).toBe('W');
  });

  it('uploadDocument() posts multipart form data with a "file" part and reports progress', () => {
    const file = new File(['hello'], 'photo.jpg', { type: 'image/jpeg' });
    service.uploadDocument('c1', file).subscribe();

    const req = http.expectOne({ method: 'POST', url: `${API}/claims/c1/documents` });
    expect(req.request.body instanceof FormData).toBeTrue();
    expect((req.request.body as FormData).get('file')).toEqual(file);
    expect(req.request.reportProgress).toBeTrue();
    expect((req.request.body as FormData).has('documentType')).withContext('omitted when no type is given').toBeFalse();
    req.flush({});
  });

  it('uploadDocument() sends the document type as the "documentType" form field', () => {
    const file = new File(['%PDF'], 'report.pdf', { type: 'application/pdf' });
    service.uploadDocument('c1', file, DocumentType.PoliceReport).subscribe();
    service.uploadDocument('c1', file, DocumentType.Other).subscribe();

    const [police, other] = http.match({ method: 'POST', url: `${API}/claims/c1/documents` });
    expect((police.request.body as FormData).get('documentType')).toBe(String(DocumentType.PoliceReport));
    expect((other.request.body as FormData).get('documentType')).withContext('Other (0) is still sent').toBe('0');
    police.flush({});
    other.flush({});
  });
});
