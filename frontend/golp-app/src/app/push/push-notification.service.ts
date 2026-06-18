import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Messaging, getToken, deleteToken } from '@angular/fire/messaging';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

const FCM_TOKEN_KEY = 'golp_fcm_token';
const DEVICE_ID_KEY = 'golp_device_id';

@Injectable({ providedIn: 'root' })
export class PushNotificationService {
  private readonly messaging = inject(Messaging, { optional: true });
  private readonly http = inject(HttpClient);

  /**
   * Richiede il permesso notifiche, ottiene il token FCM e lo registra sul backend.
   * Permesso negato o qualunque errore → log silenzioso: l'app funziona comunque.
   */
  async register(): Promise<void> {
    if (!this.messaging || !environment.vapidKey) {
      return;
    }
    try {
      const token = await this.fetchToken();
      if (!token) {
        return;
      }
      await firstValueFrom(
        this.http.post<void>(`${environment.apiUrl}/api/push/token`, { token, deviceId: this.getDeviceId() })
      );
      localStorage.setItem(FCM_TOKEN_KEY, token);
    } catch (err) {
      console.debug('Push registration skipped:', err);
    }
  }

  /** Invalida il token FCM e lo rimuove dal backend. Errori silenziosi. */
  async unregister(): Promise<void> {
    const token = localStorage.getItem(FCM_TOKEN_KEY);
    // La DELETE parte subito (subscription sincrona): se chiamata dal logout,
    // l'interceptor cattura il JWT prima che venga rimosso da localStorage.
    const backendCleanup = token
      ? firstValueFrom(this.http.delete<void>(`${environment.apiUrl}/api/push/token`, { body: { token } }))
      : Promise.resolve();
    try {
      if (this.messaging) {
        await this.removeToken();
      }
      await backendCleanup;
    } catch (err) {
      console.debug('Push unregistration skipped:', err);
    } finally {
      localStorage.removeItem(FCM_TOKEN_KEY);
    }
  }

  /**
   * Wrapper su getToken: isolato per essere spiabile nei test.
   * Niente registrazione manuale del SW: getToken registra da solo
   * /firebase-messaging-sw.js nello scope dedicato, senza sovrascrivere ngsw-worker.
   */
  protected fetchToken(): Promise<string | null> {
    return getToken(this.messaging!, { vapidKey: environment.vapidKey });
  }

  /** Wrapper su deleteToken: isolato per essere spiabile nei test. */
  protected removeToken(): Promise<boolean> {
    return deleteToken(this.messaging!);
  }

  private getDeviceId(): string {
    let id = localStorage.getItem(DEVICE_ID_KEY);
    if (!id) {
      id = crypto.randomUUID();
      localStorage.setItem(DEVICE_ID_KEY, id);
    }
    return id;
  }
}
