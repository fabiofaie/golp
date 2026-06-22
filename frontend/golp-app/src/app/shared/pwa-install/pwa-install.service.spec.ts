import { TestBed } from '@angular/core/testing';
import { PwaInstallService } from './pwa-install.service';
import { PwaPlatformService } from './pwa-platform.service';

describe('PwaInstallService', () => {
  let service: PwaInstallService;
  let platformMock: jasmine.SpyObj<PwaPlatformService>;

  beforeEach(() => {
    localStorage.clear();
    platformMock = jasmine.createSpyObj<PwaPlatformService>('PwaPlatformService', ['isStandalone', 'isMobile', 'detectOs', 'detectBrowser']);
    platformMock.isStandalone.and.returnValue(false);
    platformMock.isMobile.and.returnValue(true);

    TestBed.configureTestingModule({
      providers: [{ provide: PwaPlatformService, useValue: platformMock }]
    });
    service = TestBed.inject(PwaInstallService);
  });

  afterEach(() => localStorage.clear());

  it('shouldShowBanner true su mobile, non standalone, non dismissed', () => {
    expect(service.shouldShowBanner()).toBe(true);
  });

  it('shouldShowBanner false se standalone (app già installata)', () => {
    platformMock.isStandalone.and.returnValue(true);
    expect(service.shouldShowBanner()).toBe(false);
  });

  it('shouldShowBanner false su desktop (non mobile)', () => {
    platformMock.isMobile.and.returnValue(false);
    expect(service.shouldShowBanner()).toBe(false);
  });

  it('shouldShowBanner false dopo dismiss, persistito in localStorage', () => {
    service.dismiss();
    expect(service.shouldShowBanner()).toBe(false);
    expect(localStorage.getItem('golp.pwaInstallDismissed')).toBe('true');
  });

  it('hasNativePrompt false di default', () => {
    expect(service.hasNativePrompt()).toBe(false);
  });

  it('cattura beforeinstallprompt e rende hasNativePrompt true', () => {
    const event = new Event('beforeinstallprompt');
    (event as any).prompt = jasmine.createSpy('prompt');
    window.dispatchEvent(event);
    expect(service.hasNativePrompt()).toBe(true);
  });

  it('triggerNativePrompt chiama prompt() sull\'evento catturato', () => {
    const promptSpy = jasmine.createSpy('prompt');
    const event = new Event('beforeinstallprompt');
    (event as any).prompt = promptSpy;
    window.dispatchEvent(event);
    service.triggerNativePrompt();
    expect(promptSpy).toHaveBeenCalled();
  });

  it('triggerNativePrompt non lancia eccezioni se nessun evento catturato', () => {
    expect(() => service.triggerNativePrompt()).not.toThrow();
  });
});
