import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

export interface CurrentUser {
  id: string;
  name: string;
  email: string;
}

@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private readonly http = inject(HttpClient);
  private readonly api = `${environment.apiUrl}/auth/me`;

  readonly currentUser = signal<CurrentUser | null>(null);

  async load(): Promise<void> {
    try {
      const user = await firstValueFrom(this.http.get<CurrentUser>(this.api));
      this.currentUser.set(user);
    } catch {
      this.currentUser.set(null);
    }
  }

  async updateName(name: string): Promise<void> {
    const updated = await firstValueFrom(
      this.http.put<CurrentUser>(this.api, { name })
    );
    this.currentUser.set(updated);
  }

  clear(): void {
    this.currentUser.set(null);
  }
}
