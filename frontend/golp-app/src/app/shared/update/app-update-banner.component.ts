import { Component, inject } from '@angular/core';
import { AsyncPipe } from '@angular/common';
import { AppUpdateService } from './app-update.service';

@Component({
  selector: 'app-update-banner',
  standalone: true,
  imports: [AsyncPipe],
  template: `
    @if (updateService.updateAvailable | async) {
      <div class="update-banner" role="status">
        <span>Nuova versione disponibile</span>
        <button type="button" (click)="updateService.activate()">Aggiorna</button>
      </div>
    }
  `,
  styles: [`
    .update-banner {
      align-items: center;
      background: var(--color-accent);
      color: #fff;
      display: flex;
      font-family: var(--font-family);
      font-size: var(--font-size-sm);
      gap: var(--sp-4);
      justify-content: center;
      padding: var(--sp-3) var(--sp-4);
      position: sticky;
      top: 0;
      width: 100%;
      z-index: 1000;
    }
    button {
      background: #fff;
      border: none;
      border-radius: var(--r-md);
      color: var(--color-accent);
      cursor: pointer;
      font-family: var(--font-family);
      font-size: var(--font-size-sm);
      font-weight: var(--font-weight-bold);
      padding: var(--sp-2) var(--sp-4);
    }
  `]
})
export class AppUpdateBannerComponent {
  readonly updateService = inject(AppUpdateService);
}
