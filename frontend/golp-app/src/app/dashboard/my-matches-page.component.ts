import { Component, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatchService, MyMatchSummary } from '../circles/match.service';

type Filter = 'all' | 'pending' | 'disputed';

@Component({
  selector: 'app-my-matches-page',
  standalone: true,
  imports: [CommonModule, RouterLink, DatePipe],
  template: `
    <div class="page">
      <header class="auth-header">
        <a routerLink="/dashboard" style="color:var(--color-text-secondary);text-decoration:none;font-size:20px;line-height:1">←</a>
        <span style="font-weight:700">Le mie partite</span>
        <div style="width:28px"></div>
      </header>

      <main style="padding:16px;display:flex;flex-direction:column;gap:12px;max-width:480px;margin:0 auto;width:100%">

        <!-- Filter tabs -->
        <div class="filter-tabs">
          <button class="filter-tab" [class.active]="filter === 'all'"      (click)="setFilter('all')">Tutte</button>
          <button class="filter-tab" [class.active]="filter === 'pending'"  (click)="setFilter('pending')">In attesa</button>
          <button class="filter-tab" [class.active]="filter === 'disputed'" (click)="setFilter('disputed')">Disputate</button>
        </div>

        <!-- Count -->
        <div style="font-size:12px;color:var(--color-text-placeholder)" *ngIf="totalCount > 0">
          {{ totalCount }} partite
        </div>

        <!-- Empty state -->
        <div *ngIf="items.length === 0 && !loading" class="empty-state">
          <span style="font-size:32px;display:block;margin-bottom:12px">🎾</span>
          Nessuna partita ancora
        </div>

        <!-- Match list -->
        <div *ngFor="let m of items" class="match-row" [ngClass]="rowClass(m)">
          <!-- Top: circle + sport + date -->
          <div class="match-row-top">
            <span class="circle-tag">{{ m.circleName }}</span>
            <span class="sport-pip">· {{ m.sport }}</span>
            <span class="match-date-small">{{ m.createdAt | date:'dd/MM/yy' }}</span>
          </div>
          <!-- Bottom: result pill + score + status pip -->
          <div class="match-row-bottom">
            <span class="result-pill" [ngClass]="resultClass(m)">{{ resultLabel(m) }}</span>
            <span class="score-inline">{{ scoreLabel(m) }}</span>
            <span class="status-pip" [ngClass]="statusClass(m)">{{ statusLabel(m) }}</span>
          </div>
          <!-- Delta (spans both rows) -->
          <div class="match-row-delta">
            <span class="delta-value" [ngClass]="deltaClass(m)">{{ deltaLabel(m) }}</span>
          </div>
        </div>

        <!-- Loading -->
        <div *ngIf="loading" style="text-align:center;color:var(--color-text-secondary);font-size:13px;padding:16px">
          Caricamento…
        </div>

        <!-- Load more -->
        <button *ngIf="hasMore && !loading" class="btn-load-more" (click)="loadMore()">
          Carica altre
        </button>

      </main>
    </div>
  `,
  styles: [`
    .filter-tabs {
      display: flex;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: 9999px;
      padding: 3px;
      gap: 2px;
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
    .match-row {
      display: grid;
      grid-template-columns: 1fr auto;
      grid-template-rows: auto auto;
      gap: 4px 0;
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: 8px;
      padding: 12px 16px;
      border-left-width: 3px;
    }
    .match-row.row-win      { border-left-color: #22C55E; }
    .match-row.row-loss     { border-left-color: #FF4444; }
    .match-row.row-pending  { border-left-color: #F59E0B; }
    .match-row.row-disputed { border-left-color: #FF4444; }
    .match-row-top {
      display: flex;
      align-items: center;
      gap: 8px;
      flex-wrap: wrap;
    }
    .circle-tag {
      font-size: 11px;
      font-weight: 700;
      color: var(--color-text-secondary);
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
    .sport-pip { font-size: 11px; color: var(--color-text-placeholder); }
    .match-date-small { font-size: 11px; color: var(--color-text-placeholder); margin-left: auto; }
    .match-row-bottom {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .result-pill {
      font-size: 11px;
      font-weight: 900;
      letter-spacing: 0.06em;
      padding: 2px 8px;
      border-radius: 4px;
      flex-shrink: 0;
    }
    .result-win  { background: rgba(34,197,94,0.10);  color: #22C55E; }
    .result-loss { background: rgba(255,68,68,0.10);  color: #FF4444; }
    .result-dash { background: none; color: var(--color-text-placeholder); font-weight: 400; }
    .score-inline { font-size: 13px; font-weight: 500; flex: 1; }
    .status-pip {
      font-size: 11px;
      font-weight: 500;
      padding: 2px 8px;
      border-radius: 9999px;
    }
    .status-confirmed { background: rgba(34,197,94,0.10);  color: #22C55E; }
    .status-pending   { background: rgba(245,158,11,0.10); color: #F59E0B; }
    .status-disputed  { background: rgba(255,68,68,0.10);  color: #FF4444; }
    .match-row-delta {
      grid-row: 1 / 3;
      grid-column: 2;
      display: flex;
      align-items: center;
      justify-content: flex-end;
      padding-left: 12px;
    }
    .delta-value { font-size: 18px; font-weight: 900; line-height: 1; }
    .delta-pos  { color: #22C55E; }
    .delta-neg  { color: #FF4444; }
    .delta-none { color: var(--color-text-placeholder); font-size: 14px; font-weight: 400; }
    .empty-state {
      text-align: center;
      padding: 32px 24px;
      color: var(--color-text-placeholder);
      font-size: 13px;
      border: 1px dashed var(--color-border);
      border-radius: 14px;
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
    }
  `]
})
export class MyMatchesPageComponent implements OnInit {
  items: MyMatchSummary[] = [];
  filter: Filter = 'all';
  page = 1;
  totalCount = 0;
  loading = false;
  hasMore = false;

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
    const status = this.filter === 'all' ? undefined : this.filter;
    this.matchService.getMyMatches(p, 20, status).subscribe({
      next: (result) => {
        this.items = p === 1 ? result.items : [...this.items, ...result.items];
        this.totalCount = result.totalCount;
        this.page = result.page;
        this.hasMore = this.items.length < result.totalCount;
        this.loading = false;
      },
      error: () => { this.loading = false; }
    });
  }

  rowClass(m: MyMatchSummary): string {
    if (m.status === 'pending')   return 'row-pending';
    if (m.status === 'disputed')  return 'row-disputed';
    return m.winnerTeam === m.myTeam ? 'row-win' : 'row-loss';
  }

  resultLabel(m: MyMatchSummary): string {
    if (m.status !== 'confirmed') return '—';
    return m.winnerTeam === m.myTeam ? 'WIN' : 'LOSS';
  }

  resultClass(m: MyMatchSummary): string {
    if (m.status !== 'confirmed') return 'result-dash';
    return m.winnerTeam === m.myTeam ? 'result-win' : 'result-loss';
  }

  scoreLabel(m: MyMatchSummary): string {
    return m.sets.map(s => `${s.team1Score}–${s.team2Score}`).join(' / ');
  }

  statusLabel(m: MyMatchSummary): string {
    if (m.status === 'confirmed') return 'Confermata';
    if (m.status === 'pending')   return 'In attesa';
    return 'Disputata';
  }

  statusClass(m: MyMatchSummary): string {
    return `status-${m.status}`;
  }

  deltaLabel(m: MyMatchSummary): string {
    if (m.myDelta === null || m.myDelta === undefined) return '—';
    return m.myDelta >= 0 ? `+${m.myDelta}` : `${m.myDelta}`;
  }

  deltaClass(m: MyMatchSummary): string {
    if (m.myDelta === null || m.myDelta === undefined) return 'delta-none';
    return m.myDelta >= 0 ? 'delta-pos' : 'delta-neg';
  }
}
