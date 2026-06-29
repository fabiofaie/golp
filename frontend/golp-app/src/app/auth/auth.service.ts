import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { tap } from 'rxjs/operators';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { PushNotificationService } from '../push/push-notification.service';

interface AuthResponse { accessToken: string; refreshToken: string; }
interface RefreshResponse { accessToken: string; refreshToken: string; }
interface RegisterRequest { name: string; email: string; password: string; }
interface LoginRequest { email: string; password: string; }

const TOKEN_KEY = 'golp_token';
const REFRESH_TOKEN_KEY = 'golp_refresh_token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = `${environment.apiUrl}/auth`;
  private readonly pushService = inject(PushNotificationService);
  readonly isAuthenticated = signal(this.hasValidToken());

  constructor(private http: HttpClient) {}

  register(data: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/register`, data).pipe(
      tap(r => {
        this.storeTokens(r.accessToken, r.refreshToken);
        void this.pushService.register();
      })
    );
  }

  login(data: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/login`, data).pipe(
      tap(r => {
        this.storeTokens(r.accessToken, r.refreshToken);
        void this.pushService.register();
      })
    );
  }

  refresh(): Observable<RefreshResponse> {
    const refreshToken = this.getRefreshToken();
    return this.http.post<RefreshResponse>(`${this.api}/refresh`, { refreshToken }).pipe(
      tap(r => this.storeTokens(r.accessToken, r.refreshToken))
    );
  }

  requestPasswordReset(email: string): Observable<void> {
    return this.http.post<void>(`${this.api}/password-reset/request`, { email });
  }

  confirmPasswordReset(token: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${this.api}/password-reset/confirm`, { token, newPassword });
  }

  logout(): void {
    const refreshToken = this.getRefreshToken();
    if (refreshToken) {
      this.http.post(`${this.api}/logout`, { refreshToken }).subscribe({ error: () => {} });
    }
    // Prima della rimozione del JWT: la DELETE del token push parte con auth valida
    void this.pushService.unregister();
    this.clearLocalSession();
  }

  logoutAllDevices(): Observable<void> {
    return this.http.post<void>(`${this.api}/logout-all`, {}).pipe(
      tap(() => {
        void this.pushService.unregister();
        this.clearLocalSession();
      })
    );
  }

  deleteAccount(password: string): Observable<void> {
    return this.http.post<void>(`${this.api}/me/delete`, { password }).pipe(
      tap(() => {
        void this.pushService.unregister();
        this.clearLocalSession();
      })
    );
  }

  private clearLocalSession(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    this.isAuthenticated.set(false);
  }

  getToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  getRefreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  getCurrentUserId(): string | null {
    const token = this.getToken();
    if (!token) return null;
    try {
      return JSON.parse(atob(token.split('.')[1])).sub ?? null;
    } catch {
      return null;
    }
  }

  private storeTokens(accessToken: string, refreshToken: string): void {
    localStorage.setItem(TOKEN_KEY, accessToken);
    localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
    this.isAuthenticated.set(true);
  }

  private hasValidToken(): boolean {
    // Refresh token present = session alive; interceptor handles 401+refresh on first API call
    if (localStorage.getItem(REFRESH_TOKEN_KEY)) return true;
    const token = localStorage.getItem(TOKEN_KEY);
    if (!token) return false;
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      return payload.exp * 1000 > Date.now();
    } catch {
      return false;
    }
  }
}
