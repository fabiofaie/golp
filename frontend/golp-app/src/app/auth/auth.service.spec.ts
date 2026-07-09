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

describe('AuthService — impersonazione super admin (US-059)', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    const pushMock = jasmine.createSpyObj('PushNotificationService', ['register', 'unregister']);
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
    localStorage.removeItem('golp_refresh_token');
    localStorage.removeItem('golp_pre_impersonation_token');
    localStorage.removeItem('golp_pre_impersonation_refresh');
  });

  afterEach(() => {
    localStorage.removeItem('golp_token');
    localStorage.removeItem('golp_refresh_token');
    localStorage.removeItem('golp_pre_impersonation_token');
    localStorage.removeItem('golp_pre_impersonation_refresh');
    httpMock.verify();
  });

  const jwtWith = (payload: object) => [
    btoa(JSON.stringify({ alg: 'HS256' })),
    btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 3600, ...payload })),
    'sig',
  ].join('.');

  it('isImpersonating() ritorna false senza claim impersonator_id', () => {
    localStorage.setItem('golp_token', jwtWith({ sub: 'user-1', email: 'a@b.com' }));

    expect(service.isImpersonating()).toBeFalse();
  });

  it('isImpersonating() ritorna true con claim impersonator_id', () => {
    localStorage.setItem('golp_token', jwtWith({ sub: 'user-1', impersonator_id: 'admin-1' }));

    expect(service.isImpersonating()).toBeTrue();
  });

  it('getImpersonatedEmail() ritorna l\'email del target durante impersonazione', () => {
    localStorage.setItem('golp_token', jwtWith({ sub: 'user-1', email: 'target@b.com', impersonator_id: 'admin-1' }));

    expect(service.getImpersonatedEmail()).toBe('target@b.com');
  });

  it('isSuperAdmin() ritorna true con claim super_admin', () => {
    localStorage.setItem('golp_token', jwtWith({ sub: 'admin-1', super_admin: 'true' }));

    expect(service.isSuperAdmin()).toBeTrue();
  });

  it('startImpersonation() sostituisce i token in localStorage', () => {
    const targetJwt = jwtWith({ sub: 'user-1', email: 'target@b.com', impersonator_id: 'admin-1' });

    service.startImpersonation('target@b.com').subscribe();

    const req = httpMock.expectOne('/admin/impersonate');
    expect(req.request.body).toEqual({ email: 'target@b.com' });
    req.flush({ accessToken: targetJwt, refreshToken: 'refresh-target' });

    expect(localStorage.getItem('golp_token')).toBe(targetJwt);
    expect(localStorage.getItem('golp_refresh_token')).toBe('refresh-target');
    expect(service.isImpersonating()).toBeTrue();
  });

  it('startImpersonation() con email inesistente → errore propagato, token non toccato', () => {
    localStorage.setItem('golp_token', jwtWith({ sub: 'admin-1', super_admin: 'true' }));

    service.startImpersonation('nobody@b.com').subscribe({ error: () => {} });

    httpMock.expectOne('/admin/impersonate').flush(
      { error: 'Utente non trovato' }, { status: 404, statusText: 'Not Found' });

    expect(service.isImpersonating()).toBeFalse();
  });

  it('startImpersonation() salva i token originali del super admin', () => {
    const adminJwt = jwtWith({ sub: 'admin-1', super_admin: 'true' });
    localStorage.setItem('golp_token', adminJwt);
    localStorage.setItem('golp_refresh_token', 'refresh-admin');

    const targetJwt = jwtWith({ sub: 'user-1', email: 'target@b.com', impersonator_id: 'admin-1' });
    service.startImpersonation('target@b.com').subscribe();
    httpMock.expectOne('/admin/impersonate').flush({ accessToken: targetJwt, refreshToken: 'refresh-target' });

    expect(localStorage.getItem('golp_pre_impersonation_token')).toBe(adminJwt);
    expect(localStorage.getItem('golp_pre_impersonation_refresh')).toBe('refresh-admin');
  });

  it('endImpersonation() ripristina i token originali su successo del backend', () => {
    const adminJwt = jwtWith({ sub: 'admin-1', super_admin: 'true' });
    localStorage.setItem('golp_pre_impersonation_token', adminJwt);
    localStorage.setItem('golp_pre_impersonation_refresh', 'refresh-admin');
    localStorage.setItem('golp_token', jwtWith({ sub: 'user-1', impersonator_id: 'admin-1' }));
    localStorage.setItem('golp_refresh_token', 'refresh-target');

    service.endImpersonation().subscribe();
    httpMock.expectOne('/admin/impersonate/end').flush({});

    expect(localStorage.getItem('golp_token')).toBe(adminJwt);
    expect(localStorage.getItem('golp_refresh_token')).toBe('refresh-admin');
    expect(localStorage.getItem('golp_pre_impersonation_token')).toBeNull();
    expect(service.isImpersonating()).toBeFalse();
  });

  it('endImpersonation() ripristina comunque i token originali se la chiamata backend fallisce', () => {
    const adminJwt = jwtWith({ sub: 'admin-1', super_admin: 'true' });
    localStorage.setItem('golp_pre_impersonation_token', adminJwt);
    localStorage.setItem('golp_pre_impersonation_refresh', 'refresh-admin');
    localStorage.setItem('golp_token', jwtWith({ sub: 'user-1', impersonator_id: 'admin-1' }));
    localStorage.setItem('golp_refresh_token', 'refresh-target');

    service.endImpersonation().subscribe();
    httpMock.expectOne('/admin/impersonate/end').flush(
      { error: 'server error' }, { status: 500, statusText: 'Internal Server Error' });

    expect(localStorage.getItem('golp_token')).toBe(adminJwt);
    expect(service.isImpersonating()).toBeFalse();
  });
});
