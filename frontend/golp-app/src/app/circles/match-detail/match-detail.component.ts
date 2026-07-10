import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { MatchService, MatchDetail } from '../match.service';
import { AuthService } from '../../auth/auth.service';

type UiState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-match-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './match-detail.component.html',
})
export class MatchDetailComponent implements OnInit {
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

  isSuperAdmin       = false;
  showDeleteConfirm  = false;
  deleteInProgress   = false;
  deleteError        = '';

  showEditForm  = false;
  editSets: { team1: number; team2: number }[] = [];
  editInProgress = false;
  editError      = '';

  ngOnInit(): void {
    this.circleId      = this.route.snapshot.paramMap.get('circleId') ?? '';
    this.matchId       = this.route.snapshot.paramMap.get('matchId') ?? '';
    this.currentUserId = this.authSvc.getCurrentUserId() ?? '';
    this.isSuperAdmin  = this.authSvc.isSuperAdmin();

    this.matchSvc.getMatchDetail(this.circleId, this.matchId).subscribe({
      next: m => { this.match = m; this.state = 'ready'; },
      error: () => { this.state = 'error'; this.errorMessage = 'Partita non trovata o accesso non consentito.'; },
    });
  }

  openDeleteConfirm(): void {
    this.deleteError = '';
    this.showDeleteConfirm = true;
  }

  cancelDelete(): void {
    this.showDeleteConfirm = false;
  }

  confirmDelete(): void {
    this.deleteInProgress = true;
    this.deleteError = '';
    this.matchSvc.deleteMatchAsSuperAdmin(this.circleId, this.matchId).subscribe({
      next: () => {
        this.deleteInProgress = false;
        void this.router.navigate(['/circles', this.circleId, 'matches']);
      },
      error: () => {
        this.deleteInProgress = false;
        this.deleteError = 'Cancellazione fallita. Riprova.';
      },
    });
  }

  openEditForm(): void {
    this.editError = '';
    this.editSets = (this.match?.sets ?? []).map(s => ({ team1: s.team1Score, team2: s.team2Score }));
    if (this.editSets.length === 0)
      this.editSets = [{ team1: 0, team2: 0 }];
    this.showEditForm = true;
  }

  cancelEdit(): void {
    this.showEditForm = false;
  }

  confirmEdit(): void {
    this.editInProgress = true;
    this.editError = '';
    this.matchSvc.editMatchResultAsSuperAdmin(this.circleId, this.matchId, this.editSets).subscribe({
      next: () => {
        this.editInProgress = false;
        this.showEditForm = false;
        this.matchSvc.getMatchDetail(this.circleId, this.matchId).subscribe({
          next: m => { this.match = m; },
        });
      },
      error: () => {
        this.editInProgress = false;
        this.editError = 'Modifica fallita. Verifica i punteggi e riprova.';
      },
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

  tokenUrlFor(userId: string): string | null {
    return this.match?.confirmationLinks?.find(l => l.userId === userId)?.tokenUrl ?? null;
  }
}
