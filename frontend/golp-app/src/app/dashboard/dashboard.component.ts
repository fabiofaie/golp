import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="page">
      <header class="auth-header">
        <span class="brand">GOLP</span>
        <button (click)="logout()" style="background:none;border:none;color:var(--color-text-secondary);cursor:pointer;font-size:13px">Esci</button>
      </header>
      <main class="auth-main">
        <h1 class="auth-title">Dashboard</h1>
        <p class="auth-subtitle">Cosa vuoi fare oggi?</p>
        <div style="display:flex;flex-direction:column;gap:12px;margin-top:8px;">
          <a routerLink="/circles" class="btn-ghost">I miei circoli</a>
          <a routerLink="/circles/new" class="btn-ghost">+ Nuovo circolo</a>
        </div>
      </main>
    </div>
  `
})
export class DashboardComponent {
  constructor(private auth: AuthService, private router: Router) {}

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
