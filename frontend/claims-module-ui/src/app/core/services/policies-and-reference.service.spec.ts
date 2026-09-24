import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PoliciesService } from './policies.service';
import { ReferenceDataService } from './reference-data.service';
import { API } from '../../testing/fixtures';

describe('PoliciesService and ReferenceDataService (F-03)', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('PoliciesService.search() encodes the term as ?searchTerm=', () => {
    TestBed.inject(PoliciesService).search('POL 2026&x').subscribe();

    const req = http.expectOne((r) => r.url === `${API}/policies/search`);
    expect(req.request.params.get('searchTerm')).toBe('POL 2026&x');
    expect(req.request.urlWithParams).toBe(`${API}/policies/search?searchTerm=POL%202026%26x`);
    req.flush([]);
  });

  it('PoliciesService.getCoverage() uses /policies/{id}/coverage', () => {
    TestBed.inject(PoliciesService).getCoverage('p1').subscribe();
    const req = http.expectOne({ method: 'GET', url: `${API}/policies/p1/coverage` });
    expect(req.request.params.keys()).toEqual([]);
    req.flush([]);
  });

  it('ReferenceDataService.listCauseOfLossCodes() adds perilCategory only when given', () => {
    const service = TestBed.inject(ReferenceDataService);
    service.listCauseOfLossCodes().subscribe();
    service.listCauseOfLossCodes('Weather').subscribe();

    const [all, filtered] = http.match((r) => r.url === `${API}/reference/cause-of-loss-codes`);
    expect(all.request.params.keys()).toEqual([]);
    expect(filtered.request.params.get('perilCategory')).toBe('Weather');
    all.flush([]);
    filtered.flush([]);
  });

  it('ReferenceDataService.listClaimStatuses() uses /reference/claim-statuses', () => {
    TestBed.inject(ReferenceDataService).listClaimStatuses().subscribe();
    const req = http.expectOne({ method: 'GET', url: `${API}/reference/claim-statuses` });
    expect(req.request.params.keys()).toEqual([]);
    req.flush([]);
  });
});
