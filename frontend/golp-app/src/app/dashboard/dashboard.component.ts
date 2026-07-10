import { Component, OnInit, inject, signal, computed, effect } from '@angular/core';
import { RouterLink, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { AppVersionComponent } from '../shared/version/app-version.component';
import { ActiveCircleService } from '../shared/active-circle.service';
import { MatchService, MyMatchSummary, MatchSummary } from '../circles/match.service';
import { computeCurrentWinStreak, didUserWin } from './dashboard.utils';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink, AppVersionComponent],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly matchService = inject(MatchService);
  readonly activeCircleService = inject(ActiveCircleService);

  readonly urgentMatches = signal<MyMatchSummary[]>([]);
  readonly recentMatches = signal<MatchSummary[]>([]);

  readonly loading = this.activeCircleService.loading;
  readonly circles = this.activeCircleService.circles;
  readonly activeCircle = this.activeCircleService.activeCircle;

  readonly winStreak = computed(() => {
    const userId = this.auth.getCurrentUserId();
    if (!userId) return 0;
    return computeCurrentWinStreak(this.recentMatches(), userId);
  });

  private lastFetchedCircleId: string | null = null;

  constructor() {
    effect(() => {
      const active = this.activeCircleService.activeCircle();
      if (!active || active.id === this.lastFetchedCircleId) return;
      this.lastFetchedCircleId = active.id;
      this.matchService.getMatches(active.id).subscribe(matches => {
        this.recentMatches.set(matches.filter(m => m.status === 'confirmed'));
      });
    });
  }

  ngOnInit(): void {
    this.activeCircleService.ensureLoaded();

    // Azioni urgenti: tutte le pending dell'utente, cross-circolo (mai filtrate sul circolo attivo)
    this.matchService.getMyMatches(1, 20, 'pending').subscribe(page => {
      this.urgentMatches.set(page.items);
    });
  }

  didWin(match: MatchSummary): boolean {
    const userId = this.auth.getCurrentUserId();
    return !!userId && didUserWin(match, userId) === true;
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
