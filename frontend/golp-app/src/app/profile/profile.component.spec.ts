import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ProfileComponent } from './profile.component';
import { ThemeService } from '../theme/theme.service';
import { PushNotificationService } from '../push/push-notification.service';
import { PwaPlatformService } from '../shared/pwa-install/pwa-platform.service';

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

  function setup(): void {
    TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [
        provideRouter([]),
        { provide: PushNotificationService, useClass: FakePushNotificationService },
        { provide: PwaPlatformService, useClass: FakePwaPlatformService },
      ]
    });
    fixture = TestBed.createComponent(ProfileComponent);
    theme = TestBed.inject(ThemeService);
    push = TestBed.inject(PushNotificationService) as unknown as FakePushNotificationService;
    platform = TestBed.inject(PwaPlatformService) as unknown as FakePwaPlatformService;
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
});
