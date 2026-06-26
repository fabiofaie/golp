import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { signal } from '@angular/core';
import { ProfileComponent } from './profile.component';
import { ThemeService } from '../theme/theme.service';
import { PushNotificationService } from '../push/push-notification.service';
import { PwaPlatformService } from '../shared/pwa-install/pwa-platform.service';
import { AuthService } from '../auth/auth.service';
import { CurrentUserService, CurrentUser } from '../auth/current-user.service';

const MOCK_USER: CurrentUser = { id: '1', name: 'Mario', email: 'mario@test.com' };

function makeFakeCurrentUserService(user: CurrentUser | null = MOCK_USER) {
  const userSignal = signal(user);
  return {
    currentUser: userSignal,
    load: jasmine.createSpy('load').and.callFake(async () => { userSignal.set(MOCK_USER); }),
    updateName: jasmine.createSpy('updateName').and.callFake(async (name: string) => {
      userSignal.set({ ...MOCK_USER, name });
    }),
    clear: jasmine.createSpy('clear'),
  };
}

class FakePushNotificationService {
  supported = true;
  active = false;
  registerResult: 'granted' | 'denied' = 'granted';
  testResult = true;

  isSupported(): boolean {
    return this.supported;
  }

  isActive(): boolean {
    return this.active;
  }

  async register(): Promise<void> {
    this.active = this.registerResult === 'granted';
  }

  async unregister(): Promise<void> {
    this.active = false;
  }

  async sendTestNotification(): Promise<boolean> {
    return this.testResult;
  }
}

class FakePwaPlatformService {
  standalone = true;
  isStandalone(): boolean {
    return this.standalone;
  }
  detectOs(): 'ios' | 'android' | 'other' {
    return 'other';
  }
  detectBrowser(): 'safari' | 'chrome' | 'samsung' | 'firefox' | 'other' {
    return 'chrome';
  }
}

describe('ProfileComponent', () => {
  let fixture: ComponentFixture<ProfileComponent>;
  let theme: ThemeService;
  let push: FakePushNotificationService;
  let platform: FakePwaPlatformService;
  let authMock: jasmine.SpyObj<AuthService>;
  let currentUserMock: ReturnType<typeof makeFakeCurrentUserService>;
  let router: Router;

  function setup(): void {
    authMock = jasmine.createSpyObj('AuthService', ['logoutAllDevices', 'deleteAccount']);
    authMock.logoutAllDevices.and.returnValue(of(undefined));
    authMock.deleteAccount.and.returnValue(of(undefined));
    currentUserMock = makeFakeCurrentUserService();

    TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [
        provideRouter([]),
        { provide: PushNotificationService, useClass: FakePushNotificationService },
        { provide: PwaPlatformService, useClass: FakePwaPlatformService },
        { provide: AuthService, useValue: authMock },
        { provide: CurrentUserService, useValue: currentUserMock },
      ]
    });
    fixture = TestBed.createComponent(ProfileComponent);
    theme = TestBed.inject(ThemeService);
    push = TestBed.inject(PushNotificationService) as unknown as FakePushNotificationService;
    platform = TestBed.inject(PwaPlatformService) as unknown as FakePwaPlatformService;
    router = TestBed.inject(Router);
  }

  beforeEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove('theme-light');
    setup();
    fixture.detectChanges();
  });

  afterEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove('theme-light');
  });

  function options(): HTMLButtonElement[] {
    return Array.from(fixture.nativeElement.querySelectorAll('.theme-option'));
  }

  function pushToggle(): HTMLButtonElement | null {
    return fixture.nativeElement.querySelector('.push-toggle');
  }

  it('con localStorage vuoto mostra "Scuro" come attivo', () => {
    const [dark, light] = options();
    expect(dark.classList).toContain('theme-option--active');
    expect(light.classList).not.toContain('theme-option--active');
  });

  it('click su "Chiaro" imposta il tema light, persiste e applica la classe', () => {
    const [, light] = options();
    light.click();
    fixture.detectChanges();
    TestBed.flushEffects();

    expect(theme.theme()).toBe('light');
    expect(localStorage.getItem('golp_theme')).toBe('light');
    expect(document.documentElement.classList.contains('theme-light')).toBe(true);
    expect(light.classList).toContain('theme-option--active');
  });

  it('click su "Scuro" dopo "Chiaro" torna a dark e rimuove la classe', () => {
    const [dark, light] = options();
    light.click();
    fixture.detectChanges();
    dark.click();
    fixture.detectChanges();
    TestBed.flushEffects();

    expect(theme.theme()).toBe('dark');
    expect(document.documentElement.classList.contains('theme-light')).toBe(false);
  });

  describe('notifiche push — installato + supportato', () => {
    it('mostra il toggle off quando isActive() è false', () => {
      const toggle = pushToggle();
      expect(toggle).not.toBeNull();
      expect(toggle!.classList).not.toContain('push-toggle--active');
      expect(toggle!.textContent).toContain('Disattivate');
    });

    it('click su toggle off → register() chiamato, toggle diventa attivo', async () => {
      const toggle = pushToggle()!;
      toggle.click();
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();

      expect(pushToggle()!.classList).toContain('push-toggle--active');
      expect(pushToggle()!.textContent).toContain('Attive');
    });

    it('click su toggle off con permesso negato → resta off + messaggio guida', async () => {
      push.registerResult = 'denied';
      const toggle = pushToggle()!;
      toggle.click();
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();

      expect(pushToggle()!.classList).not.toContain('push-toggle--active');
      expect(fixture.nativeElement.querySelector('.push-hint--warn')).not.toBeNull();
    });

    it('click su toggle attivo → unregister() chiamato, toggle torna off', async () => {
      push.active = true;
      fixture = TestBed.createComponent(ProfileComponent);
      fixture.detectChanges();
      const toggle = pushToggle()!;
      expect(toggle.classList).toContain('push-toggle--active');

      toggle.click();
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();

      expect(pushToggle()!.classList).not.toContain('push-toggle--active');
    });

    it('quando attivo, mostra pulsante "Invia notifica di test"', () => {
      push.active = true;
      fixture = TestBed.createComponent(ProfileComponent);
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('.btn-test')).not.toBeNull();
    });

    it('click su "Invia notifica di test" mostra messaggio di esito', async () => {
      push.active = true;
      fixture = TestBed.createComponent(ProfileComponent);
      fixture.detectChanges();

      const btn: HTMLButtonElement = fixture.nativeElement.querySelector('.btn-test');
      btn.click();
      fixture.detectChanges();
      await fixture.whenStable();
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('.push-hint').textContent).toContain('inviata');
    });
  });

  describe('notifiche push — browser non supportato', () => {
    it('mostra toggle disabilitato con messaggio invece di fallire', () => {
      push.supported = false;
      fixture = TestBed.createComponent(ProfileComponent);
      fixture.detectChanges();

      const toggle = pushToggle()!;
      expect(toggle.disabled).toBeTrue();
      expect(fixture.nativeElement.querySelector('.push-hint').textContent).toContain('non supporta');
    });
  });

  describe('notifiche push — app non installata', () => {
    it('mostra la guida di installazione invece del toggle', () => {
      platform.standalone = false;
      fixture = TestBed.createComponent(ProfileComponent);
      fixture.detectChanges();

      expect(pushToggle()).toBeNull();
      expect(fixture.nativeElement.querySelector('.btn-install')).not.toBeNull();
    });

    it('click su "Installa l\'app" apre la guida pwa-install', () => {
      platform.standalone = false;
      fixture = TestBed.createComponent(ProfileComponent);
      fixture.detectChanges();

      const btn: HTMLButtonElement = fixture.nativeElement.querySelector('.btn-install');
      btn.click();
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('app-pwa-install-guide')).not.toBeNull();
    });
  });

  describe('logout da tutti i device', () => {
    function logoutAllButton(): HTMLButtonElement | null {
      return fixture.nativeElement.querySelector('.btn-danger');
    }

    it('click su "Esci da tutti i device" mostra la conferma senza chiamare l\'API', () => {
      logoutAllButton()!.click();
      fixture.detectChanges();

      expect(authMock.logoutAllDevices).not.toHaveBeenCalled();
      expect(fixture.nativeElement.textContent).toContain('Confermi?');
    });

    it('annullare la conferma non chiama l\'API e ripristina il pulsante iniziale', () => {
      logoutAllButton()!.click();
      fixture.detectChanges();

      const cancelBtn: HTMLButtonElement = fixture.nativeElement.querySelector('.logout-all-actions .btn-test');
      cancelBtn.click();
      fixture.detectChanges();

      expect(authMock.logoutAllDevices).not.toHaveBeenCalled();
      expect(fixture.nativeElement.textContent).not.toContain('Confermi?');
    });

    it('confermare chiama logoutAllDevices() e reindirizza al login', () => {
      const navigateSpy = spyOn(router, 'navigate');
      logoutAllButton()!.click();
      fixture.detectChanges();

      const confirmBtn: HTMLButtonElement = fixture.nativeElement.querySelector('.logout-all-actions .btn-danger');
      confirmBtn.click();
      fixture.detectChanges();

      expect(authMock.logoutAllDevices).toHaveBeenCalled();
      expect(navigateSpy).toHaveBeenCalledWith(['/login']);
    });

    it('errore API mostra messaggio e non naviga', () => {
      authMock.logoutAllDevices.and.returnValue(throwError(() => new Error('fail')));
      const navigateSpy = spyOn(router, 'navigate');
      logoutAllButton()!.click();
      fixture.detectChanges();

      const confirmBtn: HTMLButtonElement = fixture.nativeElement.querySelector('.logout-all-actions .btn-danger');
      confirmBtn.click();
      fixture.detectChanges();

      expect(navigateSpy).not.toHaveBeenCalled();
      expect(fixture.nativeElement.textContent).toContain('non riuscita');
    });
  });

  describe('nome visualizzato', () => {
    it('precompila il campo dopo ngOnInit', async () => {
      await fixture.componentInstance.ngOnInit();
      expect(fixture.componentInstance.displayName).toBe('Mario');
    });

    it('nome vuoto imposta nameError senza chiamare updateName', async () => {
      fixture.componentInstance.displayName = '   ';
      await fixture.componentInstance.saveName();
      expect(currentUserMock.updateName).not.toHaveBeenCalled();
      expect(fixture.componentInstance.nameError()).toBeTruthy();
    });

    it('nome >100 char imposta nameError senza chiamare updateName', async () => {
      fixture.componentInstance.displayName = 'a'.repeat(101);
      await fixture.componentInstance.saveName();
      expect(currentUserMock.updateName).not.toHaveBeenCalled();
      expect(fixture.componentInstance.nameError()).toBeTruthy();
    });

    it('nome valido chiama updateName e mostra conferma', async () => {
      fixture.componentInstance.displayName = 'Luigi';
      await fixture.componentInstance.saveName();
      expect(currentUserMock.updateName).toHaveBeenCalledWith('Luigi');
      expect(fixture.componentInstance.nameSaved()).toBeTrue();
      expect(fixture.componentInstance.nameError()).toBeNull();
    });

    it('errore API imposta nameError', async () => {
      currentUserMock.updateName.and.rejectWith(new Error('network'));
      fixture.componentInstance.displayName = 'Luigi';
      await fixture.componentInstance.saveName();
      expect(fixture.componentInstance.nameError()).toBeTruthy();
      expect(fixture.componentInstance.nameSaved()).toBeFalse();
    });
  });

  describe('eliminazione account', () => {
    function deleteAccountButton(): HTMLButtonElement | null {
      return fixture.nativeElement.querySelector('.btn-delete-account');
    }

    function passwordInput(): HTMLInputElement | null {
      return fixture.nativeElement.querySelector('.delete-password-input');
    }

    function confirmDeleteButton(): HTMLButtonElement | null {
      return fixture.nativeElement.querySelectorAll('.logout-all-actions .btn-danger')[0] as HTMLButtonElement;
    }

    it('click su "Elimina account" mostra il campo password senza chiamare l\'API', () => {
      deleteAccountButton()!.click();
      fixture.detectChanges();

      expect(authMock.deleteAccount).not.toHaveBeenCalled();
      expect(passwordInput()).not.toBeNull();
    });

    it('pulsante di conferma resta disabilitato finché la password è vuota', () => {
      deleteAccountButton()!.click();
      fixture.detectChanges();

      expect(confirmDeleteButton()!.disabled).toBeTrue();

      passwordInput()!.value = 'mypassword';
      passwordInput()!.dispatchEvent(new Event('input'));
      fixture.detectChanges();

      expect(confirmDeleteButton()!.disabled).toBeFalse();
    });

    it('annullare non chiama l\'API e ripristina il pulsante iniziale', () => {
      deleteAccountButton()!.click();
      fixture.detectChanges();

      const cancelBtn: HTMLButtonElement = fixture.nativeElement.querySelector('.logout-all-actions .btn-test');
      cancelBtn.click();
      fixture.detectChanges();

      expect(authMock.deleteAccount).not.toHaveBeenCalled();
      expect(passwordInput()).toBeNull();
    });

    it('confermare con password corretta chiama deleteAccount() e reindirizza al login', () => {
      const navigateSpy = spyOn(router, 'navigate');
      deleteAccountButton()!.click();
      fixture.detectChanges();

      passwordInput()!.value = 'mypassword';
      passwordInput()!.dispatchEvent(new Event('input'));
      fixture.detectChanges();

      confirmDeleteButton()!.click();
      fixture.detectChanges();

      expect(authMock.deleteAccount).toHaveBeenCalledWith('mypassword');
      expect(navigateSpy).toHaveBeenCalledWith(['/login']);
    });

    it('password errata (401) mostra messaggio esplicito e non naviga', () => {
      authMock.deleteAccount.and.returnValue(throwError(() => ({ status: 401 })));
      const navigateSpy = spyOn(router, 'navigate');
      deleteAccountButton()!.click();
      fixture.detectChanges();

      passwordInput()!.value = 'wrongpassword';
      passwordInput()!.dispatchEvent(new Event('input'));
      fixture.detectChanges();

      confirmDeleteButton()!.click();
      fixture.detectChanges();

      expect(navigateSpy).not.toHaveBeenCalled();
      expect(fixture.nativeElement.textContent).toContain('Password non valida');
    });
  });
});
