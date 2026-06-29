import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { AuthService } from './auth.service';
import { PushNotificationService } from '../push/push-notification.service';

describe('AuthService — integrazione push (US-006)', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let pushMock: jasmine.SpyObj<PushNotificationService>;

  beforeEach(() => {
    pushMock = jasmine.createSpyObj('PushNotificationService', ['register', 'unregister']);
    pushMock.register.and.resolveTo();
    pushMock.unregister.and.resolveTo();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: PushNotificationService, useValue: pushMock },
      ],
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
    localStorage.removeItem('golp_token');
  });

  afterEach(() => {
    localStorage.removeItem('golp_token');
    localStorage.removeItem('golp_refresh_token');
    httpMock.verify();
  });

  // JWT fittizio con exp futuro, basta che sia decodificabile
  const fakeJwt = [
    btoa(JSON.stringify({ alg: 'HS256' })),
    btoa(JSON.stringify({ sub: 'user-1', exp: Math.floor(Date.now() / 1000) + 3600 })),
    'sig',
  ].join('.');

  it('login con successo → push register chiamato', () => {
    service.login({ email: 'a@b.com', password: 'pw' }).subscribe();

    httpMock.expectOne('/auth/login').flush({ accessToken: fakeJwt, refreshToken: 'refresh-1' });

    expect(pushMock.register).toHaveBeenCalled();
  });

  it('register con successo → push register chiamato', () => {
    service.register({ name: 'A', email: 'a@b.com', password: 'pw' }).subscribe();

    httpMock.expectOne('/auth/register').flush({ accessToken: fakeJwt, refreshToken: 'refresh-1' });

    expect(pushMock.register).toHaveBeenCalled();
  });

  it('login fallito → push register NON chiamato', () => {
    service.login({ email: 'a@b.com', password: 'wrong' }).subscribe({ error: () => {} });

    httpMock.expectOne('/auth/login').flush(
      { error: 'invalid' }, { status: 401, statusText: 'Unauthorized' });

    expect(pushMock.register).not.toHaveBeenCalled();
  });

  it('logout → push unregister chiamato e token rimosso', () => {
    localStorage.setItem('golp_token', fakeJwt);
    localStorage.setItem('golp_refresh_token', 'refresh-1');

    service.logout();
    httpMock.expectOne('/auth/logout').flush({});

    expect(pushMock.unregister).toHaveBeenCalled();
    expect(localStorage.getItem('golp_token')).toBeNull();
    expect(localStorage.getItem('golp_refresh_token')).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();
  });

  it('logoutAllDevices → push unregister chiamato e token rimosso', () => {
    localStorage.setItem('golp_token', fakeJwt);
    localStorage.setItem('golp_refresh_token', 'refresh-1');

    service.logoutAllDevices().subscribe();
    httpMock.expectOne('/auth/logout-all').flush({});

    expect(pushMock.unregister).toHaveBeenCalled();
    expect(localStorage.getItem('golp_token')).toBeNull();
    expect(localStorage.getItem('golp_refresh_token')).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();
  });

  it('logoutAllDevices fallito → token NON rimosso, errore propagato', () => {
    localStorage.setItem('golp_token', fakeJwt);
    localStorage.setItem('golp_refresh_token', 'refresh-1');

    service.logoutAllDevices().subscribe({ error: () => {} });
    httpMock.expectOne('/auth/logout-all').flush(
      { error: 'server error' }, { status: 500, statusText: 'Internal Server Error' });

    expect(localStorage.getItem('golp_token')).toBe(fakeJwt);
  });

  it('deleteAccount con successo → push unregister chiamato e token rimosso', () => {
    localStorage.setItem('golp_token', fakeJwt);
    localStorage.setItem('golp_refresh_token', 'refresh-1');

    service.deleteAccount('mypassword').subscribe();
    const req = httpMock.expectOne('/auth/me/delete');
    expect(req.request.body).toEqual({ password: 'mypassword' });
    req.flush({});

    expect(pushMock.unregister).toHaveBeenCalled();
    expect(localStorage.getItem('golp_token')).toBeNull();
    expect(localStorage.getItem('golp_refresh_token')).toBeNull();
    expect(service.isAuthenticated()).toBeFalse();
  });

  it('deleteAccount con password errata → 401, token NON rimosso', () => {
    // Usa login() per aggiornare il signal isAuthenticated correttamente
    service.login({ email: 'a@b.com', password: 'pw' }).subscribe();
    httpMock.expectOne('/auth/login').flush({ accessToken: fakeJwt, refreshToken: 'refresh-1' });

    service.deleteAccount('wrongpassword').subscribe({ error: () => {} });
    httpMock.expectOne('/auth/me/delete').flush(
      { error: 'invalid' }, { status: 401, statusText: 'Unauthorized' });

    expect(localStorage.getItem('golp_token')).toBe(fakeJwt);
    expect(service.isAuthenticated()).toBeTrue();
  });

  it('refresh → memorizza nuova coppia di token', () => {
    localStorage.setItem('golp_refresh_token', 'old-refresh');

    service.refresh().subscribe();

    const req = httpMock.expectOne('/auth/refresh');
    expect(req.request.body).toEqual({ refreshToken: 'old-refresh' });
    req.flush({ accessToken: fakeJwt, refreshToken: 'new-refresh' });

    expect(localStorage.getItem('golp_token')).toBe(fakeJwt);
    expect(localStorage.getItem('golp_refresh_token')).toBe('new-refresh');
  });
});
