import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router, provideRouter } from '@angular/router';
import { LoginComponent } from './login.component';
import { SnackbarService } from '../../shared/services/snackbar.service';
import { API } from '../../testing/fixtures';

describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let http: HttpTestingController;
  let snackbar: jasmine.SpyObj<SnackbarService>;

  /** The role buttons are plain <button>s (no Material harness), so look them up by their accessible text. */
  function roleButtons(): HTMLButtonElement[] {
    return Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button'));
  }

  function buttonLabels(): string[] {
    return roleButtons().map((b) => b.textContent!.replace(/\s+/g, ' ').replace('person', '').replace('chevron_right', '').trim());
  }

  beforeEach(() => {
    localStorage.removeItem('claims-module.currentUser');
    snackbar = jasmine.createSpyObj<SnackbarService>('SnackbarService', ['error', 'success', 'warning', 'show']);
    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]), { provide: SnackbarService, useValue: snackbar }]
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    http.verify();
    localStorage.removeItem('claims-module.currentUser');
  });

  it('lists one button per mock role, with friendly labels (incl. the Org B handler)', () => {
    expect(fixture.nativeElement.textContent).toContain('Loading roles');
    http.expectOne(`${API}/auth/users`).flush(['handler', 'supervisor', 'manager', 'orgb-handler']);
    fixture.detectChanges();

    expect(buttonLabels()).toEqual(['Sign in as Handler', 'Sign in as Supervisor', 'Sign in as Manager', 'Sign in as Handler (Org B)']);
  });

  it('falls back to the raw key for unknown roles', () => {
    http.expectOne(`${API}/auth/users`).flush(['auditor']);
    fixture.detectChanges();
    expect(buttonLabels()).toEqual(['Sign in as auditor']);
  });

  it('signs in with the role key and navigates to /claims', () => {
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    http.expectOne(`${API}/auth/users`).flush(['handler', 'orgb-handler']);
    fixture.detectChanges();

    roleButtons()[1].click();
    fixture.detectChanges();
    expect(roleButtons().every((b) => b.disabled)).withContext('buttons disabled while signing in').toBeTrue();

    const login = http.expectOne(`${API}/auth/mock-login`);
    expect(login.request.body).toEqual({ role: 'orgb-handler' });
    login.flush({ token: 't', userId: 'u', userName: 'Olga OrgB', role: 'Handler' });

    expect(navigate).toHaveBeenCalledWith(['/claims']);
  });

  it('leaves an unreachable API to the error interceptor and shows the empty state', () => {
    http.expectOne(`${API}/auth/users`).flush(null, { status: 0, statusText: 'Unknown Error' });
    fixture.detectChanges();
    expect(snackbar.error).withContext('the interceptor reports it; a second snackbar would replace its message').not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('No mock roles available');
  });
});
