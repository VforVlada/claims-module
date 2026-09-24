import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ReservesService } from './reserves.service';
import { ReserveComponentType } from '../../shared/models/enums';
import { API } from '../../testing/fixtures';

describe('ReservesService (F-03)', () => {
  let service: ReservesService;
  let http: HttpTestingController;
  const base = `${API}/claims/c1/reserves`;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ReservesService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('list() and open() use /claims/{id}/reserves', () => {
    service.list('c1').subscribe();
    service.open('c1', { componentType: ReserveComponentType.ExpenseReserve, amount: 2500, currency: 'USD' }).subscribe();

    http.expectOne({ method: 'GET', url: base }).flush([]);
    const open = http.expectOne({ method: 'POST', url: base });
    expect(open.request.body).toEqual({ componentType: ReserveComponentType.ExpenseReserve, amount: 2500, currency: 'USD' });
    open.flush({ value: {}, warnings: [] });
  });

  it('adjust(), approve() and reject() target the reserve component', () => {
    service.adjust('c1', 'r1', { amount: 7000, reason: 'Revised estimate', currency: 'USD' }).subscribe();
    service.approve('c1', 'r1', { reserveHistoryId: 'h1', managerOverrideConfirmed: true }).subscribe();
    service.reject('c1', 'r1', { reserveHistoryId: 'h2', reason: 'Too high' }).subscribe();

    // Adjust carries the NEW TOTAL plus a mandatory reason.
    expect(http.expectOne({ method: 'PUT', url: `${base}/r1` }).request.body).toEqual({ amount: 7000, reason: 'Revised estimate', currency: 'USD' });
    expect(http.expectOne({ method: 'POST', url: `${base}/r1/approve` }).request.body).toEqual({ reserveHistoryId: 'h1', managerOverrideConfirmed: true });
    expect(http.expectOne({ method: 'POST', url: `${base}/r1/reject` }).request.body).toEqual({ reserveHistoryId: 'h2', reason: 'Too high' });
  });
});
