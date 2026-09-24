import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { describeError, errorInterceptor } from './error.interceptor';
import { AuthService } from '../services/auth.service';
import { ClaimsService } from '../services/claims.service';
import { ReservesService } from '../services/reserves.service';
import { SnackbarService } from '../../shared/services/snackbar.service';
import { API, authStub } from '../../testing/fixtures';

// F-02: the error interceptor turns 400 / 403 / 500 (and friends) into friendly snackbar messages.
describe('errorInterceptor (F-02)', () => {
  let http: HttpTestingController;
  let snackbar: jasmine.SpyObj<SnackbarService>;
  let router: jasmine.SpyObj<Router>;
  let auth: ReturnType<typeof authStub>;

  beforeEach(() => {
    snackbar = jasmine.createSpyObj<SnackbarService>('SnackbarService', ['error', 'success', 'warning', 'show']);
    router = jasmine.createSpyObj<Router>('Router', ['navigate']);
    auth = authStub('Handler');

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: SnackbarService, useValue: snackbar },
        { provide: Router, useValue: router },
        { provide: AuthService, useValue: auth }
      ]
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Issues a request through the real ClaimsService and fails it with the given status/body. */
  function failWith(status: number, body: string | object): HttpErrorResponse {
    let received: HttpErrorResponse | undefined;
    TestBed.inject(ClaimsService)
      .getById('c1')
      .subscribe({ error: (e: HttpErrorResponse) => (received = e) });
    http.expectOne(`${API}/claims/c1`).flush(body, { status, statusText: 'Error' });
    expect(received).withContext('the error is re-thrown to the caller').toBeDefined();
    return received!;
  }

  it('400 ValidationError: lists the field messages', () => {
    failWith(400, {
      type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: { LossDate: ['Loss date cannot be in the future.'], 'Parties[0].Name': ["'Name' must not be empty."] }
    });

    expect(snackbar.error).toHaveBeenCalledOnceWith(
      "Please correct the highlighted fields: Loss date cannot be in the future. 'Name' must not be empty."
    );
  });

  it('400 without field errors: generic friendly message', () => {
    failWith(400, { title: 'Bad Request', status: 400 });
    expect(snackbar.error).toHaveBeenCalledOnceWith('The request was not valid. Please check your input and try again.');
  });

  it('403 with an empty ProblemDetails: permission message', () => {
    failWith(403, { type: 'https://tools.ietf.org/html/rfc9110#section-15.5.4', title: 'Forbidden', status: 403 });
    expect(snackbar.error).toHaveBeenCalledOnceWith("You don't have permission to perform this action.");
  });

  it('403 with a specific reason: permission message plus the reason', () => {
    failWith(403, { title: 'A reserve change cannot be approved by the user who requested it.', status: 403 });
    expect(snackbar.error).toHaveBeenCalledOnceWith(
      "You don't have permission to do that: A reserve change cannot be approved by the user who requested it."
    );
  });

  it('500: never leaks server details, shows the trace id as a reference', () => {
    failWith(500, { title: 'System.NullReferenceException at Foo.Bar()', status: 500, traceId: '00-abc-01' });

    const message = snackbar.error.calls.mostRecent().args[0];
    expect(message).toBe('Something went wrong on our side. Please try again in a moment (reference: 00-abc-01).');
    expect(message).not.toContain('NullReferenceException');
  });

  it('500 with a non-JSON body still produces a friendly message', () => {
    failWith(500, '<html>Server Error</html>');
    expect(snackbar.error).toHaveBeenCalledOnceWith('Something went wrong on our side. Please try again in a moment.');
  });

  it('409 invalid status transition: shows the server title', () => {
    failWith(409, { title: "Cannot transition claim from 'Draft' to 'Closed'.", status: 409, allowedNextStatuses: ['Open'] });
    expect(snackbar.error).toHaveBeenCalledOnceWith("Cannot transition claim from 'Draft' to 'Closed'.");
  });

  it('401: signs the user out and redirects to /login', () => {
    failWith(401, { title: 'Unauthorized', status: 401 });

    expect(auth.logout).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
    expect(snackbar.error).toHaveBeenCalledOnceWith('Your session has expired. Please sign in again.');
  });

  it('status 0 (API unreachable): connection message', () => {
    expect(describeError(new HttpErrorResponse({ status: 0 }))).toContain('Could not reach the Claims Module API');
  });

  describe('errors the caller handles itself', () => {
    const capTitle = "Approving this change takes the claim's aggregate reserve over $10,000,000; a manager override is required.";

    function approve(options: { callerHandlesAggregateCap?: boolean }, title: string): void {
      let failed = false;
      TestBed.inject(ReservesService)
        .approve('c1', 'r1', { reserveHistoryId: 'h1' }, options)
        .subscribe({ error: () => (failed = true) });
      http.expectOne(`${API}/claims/c1/reserves/r1/approve`).flush({ title, status: 422 }, { status: 422, statusText: 'Unprocessable Entity' });
      expect(failed).withContext('the error still reaches the caller').toBeTrue();
    }

    it('shows no snackbar for the $10M cap error when the caller offers the override', () => {
      approve({ callerHandlesAggregateCap: true }, capTitle);
      expect(snackbar.error).not.toHaveBeenCalled();
    });

    it('still shows other errors on the same request', () => {
      approve({ callerHandlesAggregateCap: true }, 'Reserve change is not pending approval.');
      expect(snackbar.error).toHaveBeenCalled();
    });

    it('shows the cap error when the caller does not handle it', () => {
      approve({}, capTitle);
      expect(snackbar.error).toHaveBeenCalled();
    });
  });
});
