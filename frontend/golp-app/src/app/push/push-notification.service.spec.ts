import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Messaging } from '@angular/fire/messaging';
import { environment } from '../../environments/environment';
import { PushNotificationService } from './push-notification.service';

describe('PushNotificationService', () => {
  let service: PushNotificationService;
  let httpMock: HttpTestingController;
  let originalVapidKey: string;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: Messaging, useValue: {} },
      ],
    });
    service = TestBed.inject(PushNotificationService);
    httpMock = TestBed.inject(HttpTestingController);
    originalVapidKey = environment.vapidKey;
    environment.vapidKey = 'test-vapid-key';
    localStorage.removeItem('golp_fcm_token');
  });

  afterEach(() => {
    environment.vapidKey = originalVapidKey;
    localStorage.removeItem('golp_fcm_token');
    httpMock.verify();
  });

  it('register: token ottenuto → POST /api/push/token con token e deviceId', async () => {
    spyOn(service as any, 'fetchToken').and.resolveTo('fcm-token-123');

    const promise = service.register();
    await Promise.resolve(); // lascia partire la richiesta HTTP

    const req = httpMock.expectOne('/api/push/token');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.token).toBe('fcm-token-123');
    expect(req.request.body.deviceId).toBeTruthy();
    req.flush(null);

    await promise;
    expect(localStorage.getItem('golp_fcm_token')).toBe('fcm-token-123');
  });

  it('register: permesso negato (getToken throws) → nessuna eccezione propagata, nessuna chiamata HTTP', async () => {
    spyOn(service as any, 'fetchToken').and.rejectWith(
      new Error('Notification permission denied')
    );

    await expectAsync(service.register()).toBeResolved();

    httpMock.expectNone('/api/push/token');
  });

  it('register: vapidKey assente → no-op silenzioso', async () => {
    environment.vapidKey = '';
    const fetchSpy = spyOn(service as any, 'fetchToken');

    await expectAsync(service.register()).toBeResolved();

    expect(fetchSpy).not.toHaveBeenCalled();
    httpMock.expectNone('/api/push/token');
  });

  it('unregister: token presente → deleteToken + DELETE /api/push/token', async () => {
    localStorage.setItem('golp_fcm_token', 'fcm-token-123');
    const removeSpy = spyOn(service as any, 'removeToken').and.resolveTo(true);

    const promise = service.unregister();
    await Promise.resolve();
    await Promise.resolve();

    const req = httpMock.expectOne('/api/push/token');
    expect(req.request.method).toBe('DELETE');
    expect(req.request.body.token).toBe('fcm-token-123');
    req.flush(null);

    await promise;
    expect(removeSpy).toHaveBeenCalled();
    expect(localStorage.getItem('golp_fcm_token')).toBeNull();
  });

  it('unregister: errore deleteToken → nessuna eccezione, token locale comunque rimosso', async () => {
    localStorage.setItem('golp_fcm_token', 'fcm-token-123');
    spyOn(service as any, 'removeToken').and.rejectWith(new Error('boom'));

    const promise = service.unregister();
    // La DELETE parte comunque (subscription sincrona prima di removeToken)
    httpMock.expectOne('/api/push/token').flush(null);

    await expectAsync(promise).toBeResolved();
    expect(localStorage.getItem('golp_fcm_token')).toBeNull();
  });

  it('isSupported: messaging presente + vapidKey configurata → true', () => {
    expect(service.isSupported()).toBeTrue();
  });

  it('isSupported: vapidKey assente → false', () => {
    environment.vapidKey = '';
    expect(service.isSupported()).toBeFalse();
  });

  it('permissionState: rispecchia Notification.permission', () => {
    spyOnProperty(Notification, 'permission').and.returnValue('granted');
    expect(service.permissionState()).toBe('granted');
  });

  it('isActive: permesso granted + token in localStorage → true', () => {
    spyOnProperty(Notification, 'permission').and.returnValue('granted');
    localStorage.setItem('golp_fcm_token', 'fcm-token-123');
    expect(service.isActive()).toBeTrue();
  });

  it('isActive: permesso granted ma nessun token → false', () => {
    spyOnProperty(Notification, 'permission').and.returnValue('granted');
    expect(service.isActive()).toBeFalse();
  });

  it('isActive: permesso non granted → false anche con token presente', () => {
    spyOnProperty(Notification, 'permission').and.returnValue('default');
    localStorage.setItem('golp_fcm_token', 'fcm-token-123');
    expect(service.isActive()).toBeFalse();
  });

  it('sendTestNotification: POST riuscito → true', async () => {
    const promise = service.sendTestNotification();
    const req = httpMock.expectOne(`${environment.apiUrl}/api/push/test`);
    expect(req.request.method).toBe('POST');
    req.flush(null);

    expect(await promise).toBeTrue();
  });

  it('sendTestNotification: POST fallito (404 nessun token) → false, nessuna eccezione', async () => {
    const promise = service.sendTestNotification();
    httpMock.expectOne(`${environment.apiUrl}/api/push/test`).flush(null, { status: 404, statusText: 'Not Found' });

    expect(await promise).toBeFalse();
  });
});
