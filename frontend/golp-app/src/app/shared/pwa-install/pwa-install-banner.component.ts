import { Component, effect, inject } from '@angular/core';
import { PwaInstallService } from './pwa-install.service';
import { PwaInstallGuideComponent } from './pwa-install-guide.component';
import { AuthService } from '../../auth/auth.service';

@Component({
  selector: 'app-pwa-install-banner',
  standalone: true,
  imports: [PwaInstallGuideComponent],
  template: `
    @if (visible) {
      <div class="install-banner" role="complementary">
        <button type="button" class="install-banner__close" (click)="onDismiss()" aria-label="Chiudi">✕</button>
        <div class="install-banner__icon">📲</div>
        <div class="install-banner__body">
          <p class="install-banner__title">Golp funziona meglio installata</p>
          <p class="install-banner__text">Accesso più rapido, notifiche partite, niente barra del browser.</p>
          <div class="install-banner__actions">
            <button type="button" class="btn-primary" (click)="showGuide = true">Scopri come</button>
            <button type="button" class="btn-ghost" (click)="onDismiss()">Non ora</button>
          </div>
        </div>
      </div>
    }
    @if (showGuide) {
      <app-pwa-install-guide (close)="showGuide = false" />
    }
  `,
  styles: [`
    .install-banner {
      position: fixed;
      left: 50%;
      bottom: 16px;
      transform: translateX(-50%);
      width: calc(100% - 32px);
      max-width: 420px;
      background: var(--color-surface-elevated);
      border: 1px solid var(--color-border);
      border-radius: var(--r-lg);
      box-shadow: 0 -4px 24px rgba(0, 0, 0, 0.45);
      padding: var(--sp-4);
      display: flex;
      gap: var(--sp-4);
      align-items: flex-start;
      z-index: 1000;
    }
    .install-banner__icon {
      width: 44px;
      height: 44px;
      border-radius: var(--r-md);
      background: var(--color-accent-dim);
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 22px;
      flex-shrink: 0;
    }
    .install-banner__body { flex: 1; min-width: 0; }
    .install-banner__title {
      font-weight: var(--font-weight-bold);
      font-size: var(--font-size-sm);
      color: var(--color-text-primary);
      margin: 0 0 2px;
    }
    .install-banner__text {
      color: var(--color-text-secondary);
      font-size: var(--font-size-xs);
      margin: 0 0 var(--sp-2);
    }
    .install-banner__actions { display: flex; gap: var(--sp-2); }
    .btn-primary {
      background: var(--color-accent);
      color: #fff;
      border: none;
      border-radius: var(--r-full);
      font-family: var(--font-family);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-bold);
      padding: 8px 14px;
      cursor: pointer;
    }
    .btn-ghost {
      background: transparent;
      border: none;
      color: var(--color-text-secondary);
      font-family: var(--font-family);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-bold);
      padding: 8px 14px;
      cursor: pointer;
    }
    .install-banner__close {
      position: absolute;
      top: 8px;
      right: 10px;
      background: none;
      border: none;
      color: var(--color-text-placeholder);
      font-size: 16px;
      cursor: pointer;
      line-height: 1;
    }
  `]
})
export class PwaInstallBannerComponent {
  private readonly installService = inject(PwaInstallService);
  private readonly authService = inject(AuthService);

  dismissed = false;
  showGuide = false;

  get visible(): boolean {
    return !this.dismissed && this.authService.isAuthenticated() && this.installService.shouldShowBanner();
  }

  constructor() {
    effect(() => {
      document.body.classList.toggle('has-pwa-install-banner', this.visible);
    });
  }

  onDismiss(): void {
    this.installService.dismiss();
    this.dismissed = true;
    document.body.classList.remove('has-pwa-install-banner');
  }
}
