import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ThemeService, Theme } from '../theme/theme.service';
import { PushNotificationService } from '../push/push-notification.service';
import { PwaPlatformService } from '../shared/pwa-install/pwa-platform.service';
import { PwaInstallGuideComponent } from '../shared/pwa-install/pwa-install-guide.component';
import { AuthService } from '../auth/auth.service';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [RouterLink, PwaInstallGuideComponent],
  template: `
    <div class="page">
      <header class="auth-header">
        <a routerLink="/dashboard" class="back-nav">← Indietro</a>
        <span class="brand">GOLP</span>
      </header>

      <main class="auth-main">
        <h1 class="auth-title">Profilo</h1>
        <p class="auth-subtitle">Le tue preferenze su questo dispositivo.</p>

        <div class="field">
          <label>Tema</label>
          <div class="theme-toggle" role="group" aria-label="Scelta tema">
            <button
              type="button"
              class="theme-option"
              [class.theme-option--active]="theme.theme() === 'dark'"
              [attr.aria-pressed]="theme.theme() === 'dark'"
              (click)="select('dark')">
              🌙 Scuro
            </button>
            <button
              type="button"
              class="theme-option"
              [class.theme-option--active]="theme.theme() === 'light'"
              [attr.aria-pressed]="theme.theme() === 'light'"
              (click)="select('light')">
              ☀️ Chiaro
            </button>
          </div>
        </div>

        <div class="field">
          <label>Notifiche push</label>

          @if (!isInstalled()) {
            <p class="push-hint">Le notifiche push funzionano solo se installi l'app sul telefono.</p>
            <button type="button" class="btn-install" (click)="showInstallGuide.set(true)">
              📲 Installa l'app
            </button>
          } @else if (!push.isSupported()) {
            <button type="button" class="push-toggle" disabled aria-disabled="true">Notifiche push</button>
            <p class="push-hint">Il browser non supporta le notifiche push.</p>
          } @else {
            <button
              type="button"
              class="push-toggle"
              [class.push-toggle--active]="pushActive()"
              [attr.aria-pressed]="pushActive()"
              [disabled]="pushBusy()"
              (click)="toggleNotifications()">
              {{ pushActive() ? '🔔 Attive' : '🔕 Disattivate' }}
            </button>

            @if (pushDeniedMessage()) {
              <p class="push-hint push-hint--warn">{{ pushDeniedMessage() }}</p>
            }

            @if (pushActive()) {
              <div class="push-test">
                <button type="button" class="btn-test" [disabled]="testBusy()" (click)="sendTest()">
                  Invia notifica di test
                </button>
                @if (testResultMessage()) {
                  <p class="push-hint">{{ testResultMessage() }}</p>
                }
              </div>
              <p class="push-hint">
                Se non ricevi le notifiche, verifica che siano abilitate sia per Golp sia a livello di sistema
                (Impostazioni → Notifiche su Android/iOS).
              </p>
            }
          }
        </div>
        <div class="field">
          <label>Sicurezza</label>

          @if (!confirmingLogoutAll()) {
            <button type="button" class="btn-danger" (click)="confirmingLogoutAll.set(true)">
              Esci da tutti i device
            </button>
          } @else {
            <p class="push-hint push-hint--warn">
              Verrai disconnesso da tutti i device, incluso questo. Confermi?
            </p>
            <div class="logout-all-actions">
              <button type="button" class="btn-test" [disabled]="logoutAllBusy()" (click)="confirmingLogoutAll.set(false)">
                Annulla
              </button>
              <button type="button" class="btn-danger" [disabled]="logoutAllBusy()" (click)="logoutAllDevices()">
                Conferma
              </button>
            </div>
          }

          @if (logoutAllError()) {
            <p class="push-hint push-hint--warn">{{ logoutAllError() }}</p>
          }

          @if (!confirmingDeleteAccount()) {
            <button type="button" class="btn-danger btn-delete-account" (click)="confirmingDeleteAccount.set(true)">
              Elimina account
            </button>
          } @else {
            <p class="push-hint push-hint--warn">
              Questa azione è irreversibile. Inserisci la password per confermare l'eliminazione dell'account.
            </p>
            <input
              type="password"
              class="delete-password-input"
              placeholder="Password"
              [disabled]="deleteAccountBusy()"
              (input)="deletePassword.set($any($event.target).value)" />
            <div class="logout-all-actions">
              <button type="button" class="btn-test" [disabled]="deleteAccountBusy()" (click)="cancelDeleteAccount()">
                Annulla
              </button>
              <button
                type="button"
                class="btn-danger"
                [disabled]="deleteAccountBusy() || !deletePassword()"
                (click)="deleteAccount()">
                Elimina definitivamente
              </button>
            </div>
          }

          @if (deleteAccountError()) {
            <p class="push-hint push-hint--warn">{{ deleteAccountError() }}</p>
          }
        </div>
      </main>

      @if (showInstallGuide()) {
        <app-pwa-install-guide (close)="showInstallGuide.set(false)" />
      }
    </div>
  `,
  styles: [`
    .theme-toggle {
      display: flex;
      gap: var(--sp-3);
    }
    .theme-option {
      flex: 1;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--r-md);
      color: var(--color-text-secondary);
      cursor: pointer;
      font-family: var(--font-family);
      font-size: var(--font-size-base);
      font-weight: var(--font-weight-med);
      padding: var(--sp-4);
      transition: border-color 0.15s, color 0.15s, background 0.15s;
    }
    .theme-option--active {
      border-color: var(--color-accent);
      color: var(--color-accent);
      background: var(--color-accent-dim);
    }
    .push-toggle {
      width: 100%;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--r-md);
      color: var(--color-text-secondary);
      cursor: pointer;
      font-family: var(--font-family);
      font-size: var(--font-size-base);
      font-weight: var(--font-weight-med);
      padding: var(--sp-4);
      transition: border-color 0.15s, color 0.15s, background 0.15s;
    }
    .push-toggle--active {
      border-color: var(--color-accent);
      color: var(--color-accent);
      background: var(--color-accent-dim);
    }
    .push-toggle:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }
    .push-hint {
      font-size: var(--font-size-xs);
      color: var(--color-text-secondary);
      margin-top: var(--sp-2);
    }
    .push-hint--warn {
      color: var(--color-accent);
    }
    .push-test {
      margin-top: var(--sp-3);
    }
    .btn-test, .btn-install {
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--r-md);
      color: var(--color-text-primary);
      cursor: pointer;
      font-family: var(--font-family);
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-med);
      padding: var(--sp-3) var(--sp-4);
    }
    .btn-test:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }
    .btn-danger {
      background: var(--color-surface);
      border: 1px solid var(--color-accent);
      border-radius: var(--r-md);
      color: var(--color-accent);
      cursor: pointer;
      font-family: var(--font-family);
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-med);
      padding: var(--sp-3) var(--sp-4);
      width: 100%;
    }
    .btn-danger:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }
    .logout-all-actions {
      display: flex;
      gap: var(--sp-3);
      margin-top: var(--sp-2);
    }
    .logout-all-actions .btn-danger,
    .logout-all-actions .btn-test {
      flex: 1;
    }
    .btn-delete-account {
      margin-top: var(--sp-3);
    }
    .delete-password-input {
      width: 100%;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--r-md);
      color: var(--color-text-primary);
      font-family: var(--font-family);
      font-size: var(--font-size-base);
      padding: var(--sp-3) var(--sp-4);
      margin-top: var(--sp-2);
    }
  `]
})
export class ProfileComponent {
  readonly theme = inject(ThemeService);
  readonly push = inject(PushNotificationService);
  private readonly platform = inject(PwaPlatformService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly isInstalled = signal(this.platform.isStandalone());
  readonly showInstallGuide = signal(false);
  readonly pushActive = signal(this.push.isActive());
  readonly pushBusy = signal(false);
  readonly pushDeniedMessage = signal<string | null>(null);
  readonly testBusy = signal(false);
  readonly testResultMessage = signal<string | null>(null);
  readonly confirmingLogoutAll = signal(false);
  readonly logoutAllBusy = signal(false);
  readonly logoutAllError = signal<string | null>(null);
  readonly confirmingDeleteAccount = signal(false);
  readonly deletePassword = signal('');
  readonly deleteAccountBusy = signal(false);
  readonly deleteAccountError = signal<string | null>(null);

  select(t: Theme): void {
    this.theme.setTheme(t);
  }

  async toggleNotifications(): Promise<void> {
    this.pushDeniedMessage.set(null);
    this.pushBusy.set(true);
    try {
      if (this.pushActive()) {
        await this.push.unregister();
        this.pushActive.set(false);
      } else {
        await this.push.register();
        const active = this.push.isActive();
        this.pushActive.set(active);
        if (!active) {
          this.pushDeniedMessage.set(
            'Permesso negato. Per riattivarlo, abilita le notifiche per questo sito dalle impostazioni del browser.'
          );
        }
      }
    } finally {
      this.pushBusy.set(false);
    }
  }

  async sendTest(): Promise<void> {
    this.testBusy.set(true);
    this.testResultMessage.set(null);
    try {
      const sent = await this.push.sendTestNotification();
      this.testResultMessage.set(sent ? 'Notifica di prova inviata.' : 'Invio non riuscito. Riprova più tardi.');
    } finally {
      this.testBusy.set(false);
    }
  }

  logoutAllDevices(): void {
    this.logoutAllError.set(null);
    this.logoutAllBusy.set(true);
    this.auth.logoutAllDevices().subscribe({
      next: () => {
        this.router.navigate(['/login']);
      },
      error: () => {
        this.logoutAllBusy.set(false);
        this.logoutAllError.set('Operazione non riuscita. Riprova più tardi.');
      },
    });
  }

  cancelDeleteAccount(): void {
    this.confirmingDeleteAccount.set(false);
    this.deletePassword.set('');
    this.deleteAccountError.set(null);
  }

  deleteAccount(): void {
    this.deleteAccountError.set(null);
    this.deleteAccountBusy.set(true);
    this.auth.deleteAccount(this.deletePassword()).subscribe({
      next: () => {
        this.router.navigate(['/login']);
      },
      error: (err: { status?: number }) => {
        this.deleteAccountBusy.set(false);
        this.deleteAccountError.set(
          err.status === 401
            ? 'Password non valida.'
            : 'Operazione non riuscita. Riprova più tardi.'
        );
      },
    });
  }
}
