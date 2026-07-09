import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../auth/auth.service';

@Component({
  selector: 'app-impersonation-banner',
  standalone: true,
  template: `
    @if (visible) {
      <div class="impersonation-banner" role="status">
        Stai impersonando <strong>{{ impersonatedEmail }}</strong>
        <button type="button" class="impersonation-banner__exit" (click)="onExit()">Esci da impersonazione</button>
      </div>
    }
  `,
  styles: [`
    .impersonation-banner {
      position: sticky;
      top: 0;
      z-index: 2000;
      width: 100%;
      text-align: center;
      padding: var(--sp-2) var(--sp-4);
      background: var(--color-warning, #b45309);
      color: #fff;
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-bold);
      display: flex;
      align-items: center;
      justify-content: center;
      gap: var(--sp-2);
      flex-wrap: wrap;
    }
    .impersonation-banner__exit {
      background: rgba(255, 255, 255, 0.2);
      border: 1px solid rgba(255, 255, 255, 0.5);
      border-radius: var(--r-full, 999px);
      color: #fff;
      font-family: var(--font-family);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-bold);
      padding: 4px 12px;
      cursor: pointer;
    }
  `]
})
export class ImpersonationBannerComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  get visible(): boolean {
    return this.authService.isImpersonating();
  }

  get impersonatedEmail(): string | null {
    return this.authService.getImpersonatedEmail();
  }

  onExit(): void {
    this.authService.endImpersonation().subscribe(() => {
      void this.router.navigate(['/dashboard']);
    });
  }
}
