import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';
import { of, throwError } from 'rxjs';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let authService: jasmine.SpyObj<AuthService>;

  beforeEach(() => {
    authService = jasmine.createSpyObj('AuthService', ['getToken', 'getRefreshToken', 'refresh', 'logout']);

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: authService },
      ],
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('attaches Authorization header when token present', () => {
    authService.getToken.and.returnValue('access-1');

    http.get('/circles').subscribe();

    const req = httpMock.expectOne('/circles');
    expect(req.request.headers.get('Authorization')).toBe('Bearer access-1');
    req.flush({});
  });

  it('on 401, refreshes token and retries the original request', () => {
    authService.getToken.and.returnValues('expired-token', 'new-token');
    authService.getRefreshToken.and.returnValue('refresh-1');
    authService.refresh.and.returnValue(of({ accessToken: 'new-token', refreshToken: 'refresh-2' }));

    let result: unknown;
    http.get('/circles').subscribe(r => (result = r));

    const firstReq = httpMock.expectOne('/circles');
    firstReq.flush({ error: 'expired' }, { status: 401, statusText: 'Unauthorized' });

    const retriedReq = httpMock.expectOne('/circles');
    expect(retriedReq.request.headers.get('Authorization')).toBe('Bearer new-token');
    retriedReq.flush({ ok: true });

    expect(result).toEqual({ ok: true });
    expect(authService.refresh).toHaveBeenCalled();
  });

  it('on 401 with failing refresh, logs out and propagates the error', () => {
    authService.getToken.and.returnValue('expired-token');
    authService.getRefreshToken.and.returnValue('refresh-1');
    authService.refresh.and.returnValue(throwError(() => new Error('refresh failed')));

    let errored = false;
    http.get('/circles').subscribe({ error: () => (errored = true) });

    const firstReq = httpMock.expectOne('/circles');
    firstReq.flush({ error: 'expired' }, { status: 401, statusText: 'Unauthorized' });

    expect(errored).toBeTrue();
    expect(authService.logout).toHaveBeenCalled();
  });

  it('does not attempt refresh for /auth/login requests', () => {
    authService.getToken.and.returnValue(null);

    http.post('/auth/login', {}).subscribe({ error: () => {} });

    const req = httpMock.expectOne('/auth/login');
    req.flush({ error: 'invalid' }, { status: 401, statusText: 'Unauthorized' });

    expect(authService.refresh).not.toHaveBeenCalled();
  });
});
