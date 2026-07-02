import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { MatchService, MatchDetail } from '../match.service';
import { AuthService } from '../../auth/auth.service';

type UiState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-match-detail',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './match-detail.component.html',
})
export class MatchDetailComponent implements OnInit {
  private readonly route    = inject(ActivatedRoute);
  private readonly matchSvc = inject(MatchService);
  private readonly authSvc  = inject(AuthService);

  circleId      = '';
  matchId       = '';
  currentUserId = '';
  match: MatchDetail | null = null;
  state: UiState = 'loading';
  errorMessage  = '';

  ngOnInit(): void {
    this.circleId      = this.route.snapshot.paramMap.get('circleId') ?? '';
    this.matchId       = this.route.snapshot.paramMap.get('matchId') ?? '';
    this.currentUserId = this.authSvc.getCurrentUserId() ?? '';

    this.matchSvc.getMatchDetail(this.circleId, this.matchId).subscribe({
      next: m => { this.match = m; this.state = 'ready'; },
      error: () => { this.state = 'error'; this.errorMessage = 'Partita non trovata o accesso non consentito.'; },
    });
  }

  scoreLabel(): string {
    if (!this.match?.sets?.length) return '';
    return this.match.sets.map(s => `${s.team1Score}-${s.team2Score}`).join('  ·  ');
  }

  teamNames(players: MatchDetail['team1']): string {
    return players.map(p => p.userId === this.currentUserId ? `${p.name} (Tu)` : p.name).join(' & ');
  }

  playerName(userId: string): string {
    if (!this.match) return '';
    const player = [...this.match.team1, ...this.match.team2].find(p => p.userId === userId);
    if (!player) return '';
    return userId === this.currentUserId ? `${player.name} (Tu)` : player.name;
  }

  allPlayers(): { userId: string; name: string }[] {
    if (!this.match) return [];
    return [...this.match.team1, ...this.match.team2];
  }
}
