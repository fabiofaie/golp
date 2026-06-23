import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ThemeService, Theme } from '../theme/theme.service';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [RouterLink],
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
      </main>
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
  `]
})
export class ProfileComponent {
  readonly theme = inject(ThemeService);

  select(t: Theme): void {
    this.theme.setTheme(t);
  }
}
