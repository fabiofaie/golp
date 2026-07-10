import { Injectable, inject, signal, computed } from '@angular/core';
import { Router } from '@angular/router';
import { CircleService, CircleSummary } from '../circles/circle.service';
import { pickActiveCircle } from '../dashboard/dashboard.utils';

/**
 * Stato del circolo attivo condiviso tra dashboard e bottom-nav (US-064).
 * Singleton: getMyCircles() viene chiamato una sola volta per sessione di navigazione.
 */
@Injectable({ providedIn: 'root' })
export class ActiveCircleService {
  private readonly router = inject(Router);
  private readonly circleService = inject(CircleService);

  readonly loading = signal(true);
  readonly circles = signal<CircleSummary[]>([]);

  readonly activeCircle = computed(() => pickActiveCircle(this.circles()));

  private loadStarted = false;

  ensureLoaded(): void {
    if (this.loadStarted) return;
    this.loadStarted = true;
    this.circleService.getMyCircles().subscribe(circles => {
      this.circles.set(circles);
      this.loading.set(false);
    });
  }

  /**
   * Il service è providedIn:'root', quindi sopravvive a logout/login nella stessa
   * sessione browser. Senza reset, dopo un logout i circoli dell'utente precedente
   * resterebbero in cache.
   */
  reset(): void {
    this.loadStarted = false;
    this.circles.set([]);
    this.loading.set(true);
  }

  // CTA "+": punta a Quick Match, che gestisce da solo scelta/creazione del circolo
  // e giocatori ospiti — non richiede un circolo attivo pre-esistente con N membri.
  onRecordMatchClick(): void {
    this.router.navigate(['/match/quick']);
  }
}
