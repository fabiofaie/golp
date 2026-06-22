import { Injectable, inject } from '@angular/core';
import { SwUpdate, VersionReadyEvent } from '@angular/service-worker';
import { BehaviorSubject } from 'rxjs';

const CHECK_DEBOUNCE_MS = 60_000;

@Injectable({ providedIn: 'root' })
export class AppUpdateService {
  private readonly swUpdate = inject(SwUpdate);
  private lastCheckAt = 0;
  private reloadFn: () => void = () => document.location.reload();

  /** Esposto solo per i test: permette di sostituire il reload reale con uno spy. */
  setReloadFn(fn: () => void): void {
    this.reloadFn = fn;
  }

  private readonly updateAvailable$ = new BehaviorSubject<boolean>(false);
  readonly updateAvailable = this.updateAvailable$.asObservable();

  constructor() {
    this.swUpdate.versionUpdates.subscribe(event => {
      if (event.type === 'VERSION_READY') {
        this.updateAvailable$.next(true);
      }
    });
  }

  triggerCheck(): void {
    if (!this.swUpdate.isEnabled) {
      return;
    }
    const now = Date.now();
    if (now - this.lastCheckAt < CHECK_DEBOUNCE_MS) {
      return;
    }
    this.lastCheckAt = now;
    this.swUpdate.checkForUpdate().catch(() => {
      // offline o errore di rete: nessun aggiornamento, nessun errore bloccante
    });
  }

  activate(): void {
    this.swUpdate.activateUpdate()
      .catch(() => {})
      .finally(() => this.reloadFn());
  }
}

export type { VersionReadyEvent };
