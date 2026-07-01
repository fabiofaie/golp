import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { AppComponent } from './app.component';
import { AppUpdateService } from './shared/update/app-update.service';
import { PushNotificationService } from './push/push-notification.service';
import { environment } from '../environments/environment';

@Component({ selector: 'app-dummy', standalone: true, template: '' })
class DummyComponent {}

describe('AppComponent', () => {
  let updateServiceMock: { triggerCheck: jasmine.Spy };
  let pushMock: jasmine.SpyObj<PushNotificationService>;

  beforeEach(async () => {
    updateServiceMock = { triggerCheck: jasmine.createSpy('triggerCheck') };
    pushMock = jasmine.createSpyObj('PushNotificationService', ['register', 'unregister', 'isSupported', 'permissionState', 'isActive']);
    pushMock.isSupported.and.returnValue(false);
    pushMock.isActive.and.returnValue(false);

    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', component: DummyComponent }]),
        { provide: AppUpdateService, useValue: updateServiceMock },
        { provide: PushNotificationService, useValue: pushMock },
      ]
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it(`should have the 'golp-app' title`, () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app.title).toEqual('golp-app');
  });

  it('should render router-outlet', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('router-outlet')).toBeTruthy();
  });

  it('chiama triggerCheck quando il documento torna visibile', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true });
    document.dispatchEvent(new Event('visibilitychange'));

    expect(updateServiceMock.triggerCheck).toHaveBeenCalled();
  });

  it('chiama triggerCheck alla fine di una navigazione', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const router = TestBed.inject(Router);

    await router.navigateByUrl('/login');

    expect(updateServiceMock.triggerCheck).toHaveBeenCalled();
  });

  it('imposta --color-brand da environment.brandColor al bootstrap', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    const val = document.documentElement.style.getPropertyValue('--color-brand');
    expect(val).toBe(environment.brandColor);
  });
});
