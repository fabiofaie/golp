import { CommonModule, DatePipe } from "@angular/common";
import { Component, OnInit } from "@angular/core";
import { RouterLink } from "@angular/router";
import { MatchService, MyMatchSummary } from "../circles/match.service";

type Filter = "all" | "pending" | "disputed";

@Component({
  selector: "app-my-matches-page",
  standalone: true,
  imports: [CommonModule, RouterLink, DatePipe],
  template: `
    <div class="page">
      <header class="auth-header">
        <a routerLink="/dashboard" class="back-nav">
          <svg width="16" height="16" viewBox="0 0 16 16" fill="none">
            <path
              d="M10 12L6 8l4-4"
              stroke="currentColor"
              stroke-width="1.5"
              stroke-linecap="round"
              stroke-linejoin="round" />
          </svg>
          Dashboard
        </a>
        <span class="brand">GOLP</span>
      </header>

      <h1 class="auth-title">Partite.</h1>
      <p class="auth-subtitle">Le tue partite in tutti i circoli</p>

      <!-- Filter tabs -->
      <div class="filter-tabs">
        <button class="filter-tab" [class.active]="filter === 'all'" (click)="setFilter('all')">Tutte</button>
        <button class="filter-tab" [class.active]="filter === 'pending'" (click)="setFilter('pending')">
          In attesa
        </button>
        <button class="filter-tab" [class.active]="filter === 'disputed'" (click)="setFilter('disputed')">
          Contestate
        </button>
      </div>

      @if (loading && items.length === 0) {
        <p style="color:var(--color-text-secondary); font-size:13px; margin-top:16px;">Caricamento…</p>
      } @else if (items.length === 0) {
        <div class="empty-state">
          <div class="empty-icon" aria-hidden="true">✓</div>
          <p class="empty-title">Nessuna partita.</p>
          <p class="empty-subtitle">Non hai ancora partite registrate.</p>
        </div>
      } @else {
        @if (actionError) {
          <div class="form-error" style="margin-bottom:12px;">{{ actionError }}</div>
        }

        <div style="display:flex; flex-direction:column; gap:16px; margin-top:16px;">
          @for (m of items; track m.matchId) {
            <div
              class="match-card"
              [class.match-card--confirmed]="m.status === 'confirmed'"
              [class.match-card--disputed]="m.status === 'disputed'">
              <!-- header: data + circolo/sport | badge -->
              <div class="match-card-header">
                <div>
                  <a
                    [routerLink]="['/circles', m.circleId, 'matches', m.matchId, 'detail']"
                    class="match-date"
                    style="text-decoration:underline;">
                    {{ m.createdAt | date: "dd/MM, HH:mm" }}
                  </a>
                  <div style="margin-top:3px; display:flex; gap:6px; align-items:center;">
                    <span
                      style="font-size:11px; font-weight:700; color:var(--color-text-secondary); text-transform:uppercase; letter-spacing:0.04em;"
                      >{{ m.circleName }}</span
                    >
                    <span style="font-size:11px; color:var(--color-text-placeholder);">· {{ m.sport }}</span>
                  </div>
                </div>
                <div style="display:flex; align-items:center; gap:6px;">
                  @if (m.status !== "confirmed") {
                    <span
                      class="status-badge"
                      [class.status-badge--disputed]="m.status === 'disputed'"
                      [class.status-badge--pending]="m.status === 'pending'">
                      {{ m.status === "disputed" ? "Contestata" : "In attesa" }}
                    </span>
                  }
                  @if (m.status === "confirmed" && m.myDelta !== null) {
                    <span
                      class="delta-badge"
                      [class.delta-badge--positive]="m.myDelta! > 0"
                      [class.delta-badge--negative]="m.myDelta! < 0"
                      [class.delta-badge--zero]="m.myDelta === 0">
                      {{ m.myDelta! >= 0 ? "+" : "" }}{{ m.myDelta }} pt
                    </span>
                  }
                </div>
              </div>

              <!-- teams -->
              <div class="teams-display">
                <div class="team-row" [class.team-row--winner]="m.winnerTeam === 1">
                  <span class="team-label team-label--team1">Team 1</span>
                  <span class="team-names">
                    @for (p of m.team1; track p.userId; let last = $last) {
                      {{ p.name }}
                      @if (p.isActivated === false) {
                        <span class="unreg-badge">(non registrato)</span>
                      }
                      @if (!last) {
                        &amp;
                      }
                    }
                  </span>
                  @if (m.winnerTeam === 1) {
                    <span class="win-tag">✓ Vince</span>
                  }
                </div>
                <div class="team-row" [class.team-row--winner]="m.winnerTeam === 2">
                  <span class="team-label team-label--team2">Team 2</span>
                  <span class="team-names">
                    @for (p of m.team2; track p.userId; let last = $last) {
                      {{ p.name }}
                      @if (p.isActivated === false) {
                        <span class="unreg-badge">(non registrato)</span>
                      }
                      @if (!last) {
                        &amp;
                      }
                    }
                  </span>
                  @if (m.winnerTeam === 2) {
                    <span class="win-tag">✓ Vince</span>
                  }
                </div>
              </div>

              <!-- confirm dots (pending only) -->
              @if (m.status === "pending") {
                <div class="confirm-progress">
                  <div class="confirm-dots">
                    @for (dot of confirmDots(m); track $index) {
                      <div
                        class="confirm-dot"
                        [class.confirm-dot--filled]="dot === 'filled'"
                        [class.confirm-dot--you]="dot === 'you'"></div>
                    }
                  </div>
                  <span class="confirm-label">{{ m.confirmationsCount }} di 4 conferme</span>
                </div>
              }

              <!-- actions -->
              @if (m.status === "pending") {
                @if (m.hasCurrentUserConfirmed) {
                  <p style="font-size:12px; color:var(--color-text-placeholder); text-align:center; padding:8px 0;">
                    Hai già confermato · in attesa degli altri
                  </p>
                } @else {
                  <div class="match-actions">
                    <button
                      class="btn-dispute"
                      [disabled]="disputing === m.matchId"
                      (click)="dispute(m.circleId, m.matchId)">
                      {{ disputing === m.matchId ? "…" : "Contesta" }}
                    </button>
                    <a [routerLink]="['/circles', m.circleId, 'matches', m.matchId]" class="btn-confirm">
                      <svg width="13" height="13" viewBox="0 0 14 14" fill="none">
                        <path
                          d="M2 7l4 4 6-6"
                          stroke="currentColor"
                          stroke-width="2"
                          stroke-linecap="round"
                          stroke-linejoin="round" />
                      </svg>
                      Conferma
                    </a>
                  </div>
                }
              }
            </div>
          }
        </div>

        @if (loading) {
          <p style="color:var(--color-text-secondary); font-size:13px; margin-top:16px; text-align:center;">
            Caricamento…
          </p>
        }

        @if (hasMore && !loading) {
          <div style="margin-top:24px;">
            <button class="btn-load-more" (click)="loadMore()">Carica altre</button>
          </div>
        }
      }
    </div>
  `,
  styles: [
    `
      .filter-tabs {
        display: flex;
        background: var(--color-surface);
        border: 1px solid var(--color-border);
        border-radius: 9999px;
        padding: 3px;
        gap: 2px;
        margin-bottom: 16px;
      }
      .filter-tab {
        flex: 1;
        text-align: center;
        padding: 6px 10px;
        border-radius: 9999px;
        font-size: 12px;
        font-weight: 500;
        color: var(--color-text-secondary);
        cursor: pointer;
        border: none;
        background: none;
      }
      .filter-tab.active {
        background: var(--color-surface-elevated);
        color: var(--color-text-primary);
        font-weight: 700;
      }
      .btn-load-more {
        background: none;
        border: 1px solid var(--color-border);
        color: var(--color-text-secondary);
        font-size: 13px;
        font-weight: 500;
        padding: 10px 24px;
        border-radius: 9999px;
        cursor: pointer;
        width: 100%;
        font-family: var(--font-family);
      }
    `
  ]
})
export class MyMatchesPageComponent implements OnInit {
  items: MyMatchSummary[] = [];
  filter: Filter = "all";
  page = 1;
  totalCount = 0;
  loading = false;
  hasMore = false;
  disputing: string | null = null;
  actionError = "";

  constructor(private matchService: MatchService) {}

  ngOnInit(): void {
    this.loadPage(1);
  }

  setFilter(f: Filter): void {
    this.filter = f;
    this.items = [];
    this.page = 1;
    this.loadPage(1);
  }

  loadMore(): void {
    this.loadPage(this.page + 1);
  }

  private loadPage(p: number): void {
    this.loading = true;
    const status = this.filter === "all" ? undefined : this.filter;
    this.matchService.getMyMatches(p, 20, status).subscribe({
      next: (result) => {
        this.items = p === 1 ? result.items : [...this.items, ...result.items];
        this.totalCount = result.totalCount;
        this.page = result.page;
        this.hasMore = this.items.length < result.totalCount;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      }
    });
  }

  confirmDots(m: MyMatchSummary): ("filled" | "you" | "empty")[] {
    const confirmed = m.confirmationsCount;
    return Array.from({ length: 4 }, (_, i) => {
      if (i >= confirmed) return "empty";
      if (m.hasCurrentUserConfirmed && i === confirmed - 1) return "you";
      return "filled";
    });
  }

  dispute(circleId: string, matchId: string): void {
    this.disputing = matchId;
    this.actionError = "";
    this.matchService.dispute(circleId, matchId).subscribe({
      next: () => {
        this.disputing = null;
        this.items = [];
        this.loadPage(1);
      },
      error: (err) => {
        this.disputing = null;
        this.actionError = err?.error?.error ?? "Errore durante la contestazione.";
      }
    });
  }
}
