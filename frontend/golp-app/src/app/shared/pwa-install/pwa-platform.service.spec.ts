import { TestBed } from '@angular/core/testing';
import { PwaPlatformService } from './pwa-platform.service';

const UA = {
  iosSafari: 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1',
  androidChrome: 'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36',
  androidSamsung: 'Mozilla/5.0 (Linux; Android 14; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) SamsungBrowser/24.0 Chrome/115.0.0.0 Mobile Safari/537.36',
  androidFirefox: 'Mozilla/5.0 (Android 14; Mobile; rv:120.0) Gecko/120.0 Firefox/120.0',
  desktopChrome: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
};

describe('PwaPlatformService', () => {
  let service: PwaPlatformService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(PwaPlatformService);
  });

  it('rileva iOS + Safari', () => {
    expect(service.detectOs(UA.iosSafari)).toBe('ios');
    expect(service.detectBrowser(UA.iosSafari)).toBe('safari');
  });

  it('rileva Android + Chrome', () => {
    expect(service.detectOs(UA.androidChrome)).toBe('android');
    expect(service.detectBrowser(UA.androidChrome)).toBe('chrome');
  });

  it('rileva Android + Samsung Internet', () => {
    expect(service.detectOs(UA.androidSamsung)).toBe('android');
    expect(service.detectBrowser(UA.androidSamsung)).toBe('samsung');
  });

  it('rileva Android + Firefox', () => {
    expect(service.detectOs(UA.androidFirefox)).toBe('android');
    expect(service.detectBrowser(UA.androidFirefox)).toBe('firefox');
  });

  it('rileva desktop come OS "other" (non mobile)', () => {
    expect(service.detectOs(UA.desktopChrome)).toBe('other');
    expect(service.isMobile(UA.desktopChrome)).toBe(false);
  });

  it('isMobile true per iOS/Android, false per other', () => {
    expect(service.isMobile(UA.iosSafari)).toBe(true);
    expect(service.isMobile(UA.androidChrome)).toBe(true);
  });

  it('isStandalone true se navigator.standalone è true (iOS)', () => {
    Object.defineProperty(window.navigator, 'standalone', { value: true, configurable: true });
    expect(service.isStandalone()).toBe(true);
    Object.defineProperty(window.navigator, 'standalone', { value: undefined, configurable: true });
  });

  it('isStandalone true se matchMedia display-mode:standalone è true', () => {
    spyOn(window, 'matchMedia').and.returnValue({ matches: true } as MediaQueryList);
    expect(service.isStandalone()).toBe(true);
  });

  it('isStandalone false se nessuna delle due condizioni è vera', () => {
    spyOn(window, 'matchMedia').and.returnValue({ matches: false } as MediaQueryList);
    expect(service.isStandalone()).toBe(false);
  });
});
