import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { loadingInterceptor } from './loading.interceptor';
import { LoadingService } from '../services/loading.service';
import { ReferenceDataService } from '../services/reference-data.service';
import { API } from '../../testing/fixtures';

// §3.7.4 loading indicators. Requests go through a real API service (UI-02 forbids importing
// HttpClient outside *.service.ts, specs included).
describe('loadingInterceptor', () => {
  let http: HttpTestingController;
  let loading: LoadingService;
  let referenceData: ReferenceDataService;

  const codesUrl = `${API}/reference/cause-of-loss-codes`;
  const statusesUrl = `${API}/reference/claim-statuses`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([loadingInterceptor])), provideHttpClientTesting()]
    });
    http = TestBed.inject(HttpTestingController);
    loading = TestBed.inject(LoadingService);
    referenceData = TestBed.inject(ReferenceDataService);
  });

  afterEach(() => http.verify());

  it('is loading while any request is in flight, and stops when the last completes', () => {
    referenceData.listCauseOfLossCodes().subscribe();
    referenceData.listClaimStatuses().subscribe();
    expect(loading.isLoading()).toBeTrue();

    http.expectOne((r) => r.url === codesUrl).flush([]);
    expect(loading.isLoading()).withContext('one request still in flight').toBeTrue();

    http.expectOne((r) => r.url === statusesUrl).flush([]);
    expect(loading.isLoading()).toBeFalse();
  });

  it('stops on an error response', () => {
    referenceData.listClaimStatuses().subscribe({ error: () => undefined });

    http.expectOne((r) => r.url === statusesUrl).flush(null, { status: 500, statusText: 'Server Error' });

    expect(loading.isLoading()).toBeFalse();
  });

  it('stops when the request is cancelled', () => {
    const subscription = referenceData.listClaimStatuses().subscribe();
    const request = http.expectOne((r) => r.url === statusesUrl);

    subscription.unsubscribe();

    expect(request.cancelled).toBeTrue();
    expect(loading.isLoading()).toBeFalse();
  });
});
