import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, ActivatedRoute, Router } from '@angular/router';
import { MatchService, MatchDetail } from '../match.service';
import { AuthService } from '../../auth/auth.service';

type UiState = 'loading' | 'ready' | 'confirming' | 'confirmed' | 'disputed' | 'error';

@Component({
  selector: 'app-match-confirm',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './match-confirm.component.html',
})
export class MatchConfirmComponent implements OnInit {
  private readonly route    = inject(ActivatedRoute);
  private readonly router   = inject(Router);
  private readonly matchSvc = inject(MatchService);
  private readonly authSvc  = inject(AuthService);

  circleId      = '';
  matchId       = '';
  currentUserId = '';
  match: MatchDetail | null = null;
  state: UiState = 'loading';
  errorMessage  = '';
  newCount      = 0;

  ngOnInit(): void {
    this.circleId      = this.route.snapshot.paramMap.get('circleId') ?? '';
    this.matchId       = this.route.snapshot.paramMap.get('matchId') ?? '';
    this.currentUserId = this.authSvc.getCurrentUserId() ?? '';
    this.load();
  }

  private load(): void {
    this.matchSvc.getMatchDetail(this.circleId, this.matchId).subscribe({
      next: m => { this.match = m; this.state = 'ready'; },
      error: () => { this.state = 'error'; this.errorMessage = 'Partita non trovata.'; },
    });
  }

  confirm(): void {
    if (!this.match || this.state === 'confirming') return;
    this.state = 'confirming';
    this.matchSvc.confirm(this.circleId, this.matchId).subscribe({
      next: r => {
        this.newCount = r.confirmationsCount ?? 0;
        this.state = r.status === 'confirmed' ? 'confirmed' : 'confirmed';
        if (this.match) {
          this.match = { ...this.match, status: r.status as any, confirmationsCount: this.newCount, hasCurrentUserConfirmed: true };
        }
      },
      error: err => {
        this.state = 'ready';
        this.errorMessage = err?.error?.error ?? 'Errore durante la conferma.';
      },
    });
  }

  dispute(): void {
    if (!this.match || this.state === 'confirming') return;
    this.state = 'confirming';
    this.matchSvc.dispute(this.circleId, this.matchId).subscribe({
      next: () => { this.state = 'disputed'; if (this.match) this.match = { ...this.match, status: 'disputed' }; },
      error: err => {
        this.state = 'ready';
        this.errorMessage = err?.error?.error ?? 'Errore durante la contestazione.';
      },
    });
  }

  backToMatches(): void {
    this.router.navigate(['/circles', this.circleId, 'matches']);
  }

  scoreLabel(): string {
    if (!this.match?.sets?.length) return '';
    return this.match.sets.map(s => `${s.team1Score}-${s.team2Score}`).join('  ·  ');
  }

  teamNames(players: MatchDetail['team1']): string {
    return players.map(p => p.userId === this.currentUserId ? `${p.name} (Tu)` : p.name).join(' & ');
  }

  dots(): ('filled' | 'you' | 'empty')[] {
    if (!this.match) return Array(4).fill('empty');
    const count = this.match.confirmationsCount;
    return Array.from({ length: 4 }, (_, i) => {
      if (i >= count) return 'empty';
      if (this.match!.hasCurrentUserConfirmed && i === count - 1) return 'you';
      return 'filled';
    });
  }
}
