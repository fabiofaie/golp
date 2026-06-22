import { Injectable } from '@angular/core';

export type PwaOs = 'ios' | 'android' | 'other';
export type PwaBrowser = 'safari' | 'chrome' | 'samsung' | 'firefox' | 'other';

@Injectable({ providedIn: 'root' })
export class PwaPlatformService {

  detectOs(userAgent: string = navigator.userAgent): PwaOs {
    if (/iPhone|iPad|iPod/i.test(userAgent)) {
      return 'ios';
    }
    if (/Android/i.test(userAgent)) {
      return 'android';
    }
    return 'other';
  }

  detectBrowser(userAgent: string = navigator.userAgent): PwaBrowser {
    if (/SamsungBrowser/i.test(userAgent)) {
      return 'samsung';
    }
    if (/FxiOS|Firefox/i.test(userAgent)) {
      return 'firefox';
    }
    if (/CriOS|Chrome/i.test(userAgent)) {
      return 'chrome';
    }
    if (/Safari/i.test(userAgent)) {
      return 'safari';
    }
    return 'other';
  }

  isMobile(userAgent: string = navigator.userAgent): boolean {
    return this.detectOs(userAgent) !== 'other';
  }

  isStandalone(): boolean {
    const nav = navigator as Navigator & { standalone?: boolean };
    return nav.standalone === true || window.matchMedia('(display-mode: standalone)').matches;
  }
}
