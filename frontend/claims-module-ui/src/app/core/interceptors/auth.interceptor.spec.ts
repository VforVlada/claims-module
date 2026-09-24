import { TestBed } from '@angular/core/testing';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from '../services/auth.service';
import { ClaimsService } from '../services/claims.service';
import { PoliciesService } from '../services/policies.service';
import { ReservesService } from '../services/reserves.service';
import { ClaimStatus, ReserveComponentType } from '../../shared/models/enums';
import { API, authStub } from '../../testing/fixtures';

// F-01: every outgoing request carries the bearer token. Requests are issued through the real API
// services (UI-02 forbids importing HttpClient outside *.service.ts, specs included).
describe('authInterceptor (F-01)', () => {
  let http: HttpTestingController;

  function setup(role: 'Handler' | null): void {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: authStub(role, 'jwt-abc') }
      ]
    });
    http = TestBed.inject(HttpTestingController);
  }

  afterEach(() => http.verify());

  it('adds Authorization: Bearer <token> to GET, POST and PUT requests', () => {
    setup('Handler');
    TestBed.inject(ClaimsService).getById('c1').subscribe();
    TestBed.inject(ClaimsService).transitionStatus('c1', ClaimStatus.UnderInvestigation).subscribe();
    TestBed.inject(ReservesService).open('c1', { componentType: ReserveComponentType.IndemnityReserve, amount: 100 }).subscribe();
    TestBed.inject(PoliciesService).search('POL').subscribe();

    const requests = http.match(() => true);
    expect(requests.map((r) => r.request.method)).toEqual(['GET', 'PUT', 'POST', 'GET']);
    for (const req of requests) {
      expect(req.request.headers.get('Authorization')).withContext(`${req.request.method} ${req.request.url}`).toBe('Bearer jwt-abc');
      req.flush({});
    }
  });

  it('adds the header to multipart document uploads too', () => {
    setup('Handler');
    TestBed.inject(ClaimsService).uploadDocument('c1', new File(['x'], 'a.txt')).subscribe();

    const req = http.expectOne(`${API}/claims/c1/documents`);
    expect(req.request.headers.get('Authorization')).toBe('Bearer jwt-abc');
    req.flush({});
  });

  it('leaves requests untouched when nobody is signed in', () => {
    setup(null);
    TestBed.inject(ClaimsService).getById('c1').subscribe();

    const req = http.expectOne(`${API}/claims/c1`);
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });
});
