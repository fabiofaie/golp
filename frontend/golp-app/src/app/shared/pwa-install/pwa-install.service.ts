import { Injectable, inject } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { PwaPlatformService } from './pwa-platform.service';

const DISMISSED_KEY = 'golp.pwaInstallDismissed';

interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>;
}

@Injectable({ providedIn: 'root' })
export class PwaInstallService {
  private readonly platform = inject(PwaPlatformService);

  private readonly nativePrompt$ = new BehaviorSubject<BeforeInstallPromptEvent | null>(null);
  readonly nativePromptAvailable = this.nativePrompt$.asObservable();

  constructor() {
    window.addEventListener('beforeinstallprompt', (event: Event) => {
      event.preventDefault();
      this.nativePrompt$.next(event as BeforeInstallPromptEvent);
    });
  }

  shouldShowBanner(): boolean {
    if (this.platform.isStandalone()) {
      return false;
    }
    if (!this.platform.isMobile()) {
      return false;
    }
    return localStorage.getItem(DISMISSED_KEY) !== 'true';
  }

  dismiss(): void {
    localStorage.setItem(DISMISSED_KEY, 'true');
  }

  hasNativePrompt(): boolean {
    return this.nativePrompt$.value !== null;
  }

  triggerNativePrompt(): void {
    this.nativePrompt$.value?.prompt();
  }
}
