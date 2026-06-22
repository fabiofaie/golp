import { Component, EventEmitter, inject, Output } from '@angular/core';
import { PwaPlatformService } from './pwa-platform.service';
import { PwaInstallService } from './pwa-install.service';
import { getInstallGuide, InstallGuideContent } from './pwa-install-steps';

@Component({
  selector: 'app-pwa-install-guide',
  standalone: true,
  template: `
    <div class="install-guide" role="dialog" aria-modal="true">
      <div class="install-guide__header">
        <button type="button" class="install-guide__back" (click)="close.emit()">← Torna indietro</button>
        <h2 class="install-guide__title">Installa Golp sul telefono</h2>
        <span class="platform-badge">📱 Rilevato: {{ content.badgeLabel }}</span>
      </div>
      <div class="install-guide__body">
        @for (step of content.steps; track $index) {
          <div class="step">
            <div class="step__num">{{ $index + 1 }}</div>
            <div class="step__text">{{ step.text }}</div>
          </div>
        }
        @if (content.hasNativePrompt && installService.hasNativePrompt()) {
          <div class="install-guide__native">
            <p>Il tuo browser supporta l'installazione diretta.</p>
            <button type="button" class="btn-native" (click)="installService.triggerNativePrompt()">Installa ora</button>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    .install-guide {
      position: fixed;
      inset: 0;
      background: var(--color-bg);
      display: flex;
      flex-direction: column;
      z-index: 1100;
    }
    .install-guide__header {
      padding: var(--sp-6) var(--sp-4) var(--sp-4);
      border-bottom: 1px solid var(--color-border);
    }
    .install-guide__back {
      background: none;
      border: none;
      color: var(--color-text-secondary);
      font-family: var(--font-family);
      font-size: var(--font-size-sm);
      cursor: pointer;
      padding: 0;
      margin-bottom: var(--sp-4);
    }
    .install-guide__title {
      font-size: var(--font-size-lg);
      font-weight: var(--font-weight-bold);
      margin: 0 0 var(--sp-3);
      color: var(--color-text-primary);
    }
    .platform-badge {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      background: var(--color-accent-dim);
      border: 1px solid rgba(255, 85, 0, 0.3);
      border-radius: var(--r-full);
      padding: 5px 12px;
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-bold);
      color: var(--color-accent);
    }
    .install-guide__body {
      flex: 1;
      overflow-y: auto;
      padding: var(--sp-6) var(--sp-4);
    }
    .step {
      display: flex;
      gap: var(--sp-4);
      margin-bottom: var(--sp-6);
    }
    .step__num {
      width: 28px;
      height: 28px;
      border-radius: 50%;
      background: var(--color-surface-elevated);
      color: var(--color-accent);
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: var(--font-weight-bold);
      font-size: var(--font-size-sm);
      flex-shrink: 0;
    }
    .step__text {
      font-size: var(--font-size-sm);
      color: var(--color-text-primary);
      padding-top: 3px;
    }
    .install-guide__native {
      margin: var(--sp-4) 0 var(--sp-6);
      padding: var(--sp-4);
      background: var(--color-success-bg);
      border: 1px solid rgba(34, 197, 94, 0.3);
      border-radius: var(--r-lg);
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: var(--sp-4);
    }
    .install-guide__native p {
      margin: 0;
      font-size: var(--font-size-xs);
      color: var(--color-text-secondary);
    }
    .btn-native {
      background: var(--color-accent);
      color: #fff;
      border: none;
      border-radius: var(--r-full);
      font-family: var(--font-family);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-bold);
      padding: 8px 14px;
      cursor: pointer;
      flex-shrink: 0;
    }
  `]
})
export class PwaInstallGuideComponent {
  private readonly platform = inject(PwaPlatformService);
  readonly installService = inject(PwaInstallService);

  @Output() close = new EventEmitter<void>();

  readonly content: InstallGuideContent = getInstallGuide(this.platform.detectOs(), this.platform.detectBrowser());
}
