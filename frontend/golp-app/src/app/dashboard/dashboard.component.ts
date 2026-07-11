import { Component, OnInit, inject, signal, computed, effect } from '@angular/core';
import { RouterLink, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { AppVersionComponent } from '../shared/version/app-version.component';
import { ActiveCircleService } from '../shared/active-circle.service';
import { ActiveCirclePanelComponent } from '../shared/active-circle-panel/active-circle-panel.component';
import { MatchSummary } from '../circles/match.service';
import { DashboardService, DashboardSummary } from './dashboard.service';
import { computeCurrentWinStreak, didUserWin } from './dashboard.utils';

export interface AggregateStats {
  circlesCount: number;
  confirmedMatchesCount: number;
  winRate: number;
  urgentCount: number;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink, AppVersionComponent, ActiveCirclePanelComponent],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly dashboardService = inject(DashboardService);
  readonly activeCircleService = inject(ActiveCircleService);

  readonly panelOpen = signal(false);

  readonly loading = this.activeCircleService.loading;
  readonly circles = this.activeCircleService.circles;
  // Circolo attivo istantaneo (per il bottone selettore): deriva dai circoli già caricati da
  // ActiveCircleService, senza attendere il round-trip di /dashboard/summary (evita un flash
  // vuoto sul bottone mentre la vista dettagliata sta ancora caricando).
  readonly activeCircle = this.activeCircleService.activeCircle;

  // US-070: un'unica chiamata a /dashboard/summary sostituisce le fetch separate di
  // ultime partite, richieste urgenti e statistiche aggregate (AC1).
  private readonly summary = signal<DashboardSummary | null>(null);

  readonly recentMatches = computed<MatchSummary[]>(() => this.summary()?.activeCircle?.recentMatches ?? []);
  readonly urgentMatches = computed(() => this.summary()?.urgentMatches ?? []);
  // Conteggio reale delle partite confirmed del circolo attivo: recentMatches è limitata alle
  // ultime 20 lato server (US-070), quindi non è un sostituto affidabile per il totale.
  readonly confirmedMatchesCount = computed(() => this.summary()?.activeCircle?.confirmedMatchesCount ?? 0);

  readonly winStreak = computed(() => {
    const userId = this.auth.getCurrentUserId();
    if (!userId) return 0;
    return computeCurrentWinStreak(this.recentMatches(), userId);
  });

  // US-067: statistiche aggregate mostrate in modalità "Tutti i circoli". Nessun rating
  // aggregato tra circoli — solo conteggi e percentuale di vittorie cross-circolo, calcolati
  // ora lato server (US-070) e composti qui solo con il conteggio urgenti (sempre locale).
  readonly aggregateStats = computed<AggregateStats>(() => {
    const aggregate = this.summary()?.aggregate;
    return {
      circlesCount: aggregate?.circlesCount ?? this.circles().length,
      confirmedMatchesCount: aggregate?.confirmedMatchesCount ?? 0,
      winRate: aggregate?.winRate ?? 0,
      urgentCount: this.urgentMatches().length,
    };
  });

  private lastFetchedKey: string | null = null;

  constructor() {
    effect(() => {
      if (this.activeCircleService.loading() || this.activeCircleService.circles().length === 0) return;

      const isAll = this.activeCircleService.isAllCirclesSelected();
      const circleId = isAll ? undefined : this.activeCircleService.activeCircle()?.id;
      const key = isAll ? 'all' : (circleId ?? null);
      if (key === null || key === this.lastFetchedKey) return;

      this.lastFetchedKey = key;
      this.dashboardService.getDashboardSummary(circleId).subscribe(s => this.summary.set(s));
    });
  }

  ngOnInit(): void {
    this.activeCircleService.ensureLoaded();
  }

  openPanel(): void {
    this.panelOpen.set(true);
  }

  closePanel(): void {
    this.panelOpen.set(false);
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
