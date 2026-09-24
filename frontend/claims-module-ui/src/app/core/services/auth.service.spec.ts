import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { API } from '../../testing/fixtures';

describe('AuthService', () => {
  const STORAGE_KEY = 'claims-module.currentUser';
  let http: HttpTestingController;

  beforeEach(() => {
    localStorage.removeItem(STORAGE_KEY);
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    localStorage.removeItem(STORAGE_KEY);
  });

  it('login() POSTs {role} to /auth/mock-login and stores the user', () => {
    const auth = TestBed.inject(AuthService);
    auth.login('supervisor').subscribe();

    const req = http.expectOne({ method: 'POST', url: `${API}/auth/mock-login` });
    expect(req.request.body).toEqual({ role: 'supervisor' });
    req.flush({ token: 't', userId: 'u', userName: 'Sam Supervisor', role: 'Supervisor' });

    expect(auth.isAuthenticated()).toBeTrue();
    expect(auth.role()).toBe('Supervisor');
    expect(auth.canApproveReserves()).toBeTrue();
    expect(JSON.parse(localStorage.getItem(STORAGE_KEY)!).token).toBe('t');
  });

  it('listRoles() GETs /auth/users', () => {
    let roles: string[] = [];
    TestBed.inject(AuthService).listRoles().subscribe((r) => (roles = r));
    http.expectOne({ method: 'GET', url: `${API}/auth/users` }).flush(['handler', 'orgb-handler']);
    expect(roles).toEqual(['handler', 'orgb-handler']);
  });

  it('logout() clears the user and storage', () => {
    const auth = TestBed.inject(AuthService);
    auth.login('handler').subscribe();
    http.expectOne(`${API}/auth/mock-login`).flush({ token: 't', userId: 'u', userName: 'H', role: 'Handler' });

    auth.logout();
    expect(auth.isAuthenticated()).toBeFalse();
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });
});
