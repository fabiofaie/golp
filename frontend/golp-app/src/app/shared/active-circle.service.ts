import { Injectable, inject, signal, computed } from '@angular/core';
import { Router } from '@angular/router';
import { CircleService, CircleSummary } from '../circles/circle.service';
import { pickActiveCircle } from '../dashboard/dashboard.utils';

const STORAGE_KEY_ACTIVE = 'golp_active_circle_id';
const STORAGE_KEY_FAVORITES = 'golp_favorite_circle_ids';
const ALL_CIRCLES = 'all';

/**
 * Stato del circolo attivo condiviso tra dashboard e bottom-nav (US-064).
 * Singleton: getMyCircles() viene chiamato una sola volta per sessione di navigazione.
 *
 * Selezione esplicita (US-066): l'utente può scegliere un circolo o "tutti i circoli"
 * dal pannello selettore. La scelta è persistita in localStorage e ha priorità sul
 * criterio automatico pickActiveCircle, usato solo come fallback quando non c'è una
 * selezione salvata o quando referenzia un circolo di cui l'utente non è più membro.
 */
@Injectable({ providedIn: 'root' })
export class ActiveCircleService {
  private readonly router = inject(Router);
  private readonly circleService = inject(CircleService);

  readonly loading = signal(true);
  readonly circles = signal<CircleSummary[]>([]);

  readonly activeSelection = signal<string | null>(readStoredValue(STORAGE_KEY_ACTIVE));
  readonly favoriteCircleIds = signal<Set<string>>(readStoredFavorites());

  readonly activeCircle = computed<CircleSummary | null>(() => {
    const selection = this.activeSelection();
    const circles = this.circles();
    if (selection === ALL_CIRCLES) return null;
    if (selection) {
      const found = circles.find(c => c.id === selection);
      if (found) return found;
    }
    return pickActiveCircle(circles);
  });

  private loadStarted = false;

  ensureLoaded(): void {
    if (this.loadStarted) return;
    this.loadStarted = true;
    this.circleService.getMyCircles().subscribe(circles => {
      this.circles.set(circles);
      this.loading.set(false);
    });
  }

  /** Selezione esplicita dal pannello selettore (US-066): un circolo o "tutti i circoli". */
  selectCircle(idOrAll: string): void {
    this.activeSelection.set(idOrAll);
    writeStoredValue(STORAGE_KEY_ACTIVE, idOrAll);
  }

  isAllCirclesSelected(): boolean {
    return this.activeSelection() === ALL_CIRCLES;
  }

  toggleFavorite(circleId: string): void {
    const next = new Set(this.favoriteCircleIds());
    if (next.has(circleId)) {
      next.delete(circleId);
    } else {
      next.add(circleId);
    }
    this.favoriteCircleIds.set(next);
    writeStoredValue(STORAGE_KEY_FAVORITES, JSON.stringify([...next]));
  }

  /**
   * Il service è providedIn:'root', quindi sopravvive a logout/login nella stessa
   * sessione browser. Senza reset, dopo un logout i circoli e la selezione dell'utente
   * precedente resterebbero in cache/localStorage.
   */
  reset(): void {
    this.loadStarted = false;
    this.circles.set([]);
    this.loading.set(true);
    this.activeSelection.set(null);
    this.favoriteCircleIds.set(new Set());
    writeStoredValue(STORAGE_KEY_ACTIVE, null);
    writeStoredValue(STORAGE_KEY_FAVORITES, null);
  }

  // CTA "+": punta a Quick Match, che gestisce da solo scelta/creazione del circolo
  // e giocatori ospiti. Se c'è un circolo attivo singolo (non "tutti i circoli"),
  // lo passa come pre-selezione opzionale via query param.
  onRecordMatchClick(): void {
    const active = this.activeCircle();
    if (active && !this.isAllCirclesSelected()) {
      this.router.navigate(['/match/quick'], { queryParams: { circleId: active.id } });
    } else {
      this.router.navigate(['/match/quick']);
    }
  }
}

function readStoredValue(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

function writeStoredValue(key: string, value: string | null): void {
  try {
    if (value === null) {
      localStorage.removeItem(key);
    } else {
      localStorage.setItem(key, value);
    }
  } catch {
    // localStorage non disponibile (es. modalità privata): la selezione resta solo in-memory
  }
}

function readStoredFavorites(): Set<string> {
  const raw = readStoredValue(STORAGE_KEY_FAVORITES);
  if (!raw) return new Set();
  try {
    return new Set(JSON.parse(raw));
  } catch {
    return new Set();
  }
}
