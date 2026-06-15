import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { MatchService, MatchSummary } from '../match.service';
import { CircleService } from '../circle.service';
import { AuthService } from '../../auth/auth.service';

@Component({
  selector: 'app-circle-match-history',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './circle-match-history.component.html',
})
export class CircleMatchHistoryComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly matchSvc = inject(MatchService);
  private readonly circleSvc = inject(CircleService);
  private readonly authSvc = inject(AuthService);

  circleId = '';
  currentUserId = '';
  isOwner = false;
  matches: MatchSummary[] = [];
  loading = true;
  errorMessage = '';

  confirming: string | null = null;
  disputing: string | null = null;
  forceConfirming: string | null = null;
  actionError = '';

  ngOnInit(): void {
    this.circleId = this.route.snapshot.paramMap.get('circleId') ?? '';
    this.currentUserId = this.authSvc.getCurrentUserId() ?? '';
    this.circleSvc.getMyCircles().subscribe({
      next: circles => {
        const circle = circles.find(c => c.id === this.circleId);
        this.isOwner = circle?.ownerId === this.currentUserId;
      },
    });
    this.loadMatches();
  }

  loadMatches(): void {
    this.loading = true;
    this.matchSvc.getMatches(this.circleId).subscribe({
      next: ms => { this.matches = ms; this.loading = false; },
      error: () => { this.errorMessage = 'Impossibile caricare le partite.'; this.loading = false; },
    });
  }

  isParticipant(m: MatchSummary): boolean {
    return [...m.team1, ...m.team2].some(p => p.userId === this.currentUserId);
  }

  teamNames(players: MatchSummary['team1']): string {
    return players.map(p => p.userId === this.currentUserId ? `${p.name} (Tu)` : p.name).join(' & ');
  }

  confirmDots(m: MatchSummary): ('filled' | 'you' | 'empty')[] {
    const confirmed = m.confirmationsCount;
    return Array.from({ length: 4 }, (_, i) => {
      if (i >= confirmed) return 'empty';
      // mark the last filled dot as "you" if user has confirmed
      if (m.hasCurrentUserConfirmed && i === confirmed - 1) return 'you';
      return 'filled';
    });
  }

  confirm(matchId: string): void {
    this.confirming = matchId;
    this.actionError = '';
    this.matchSvc.confirm(this.circleId, matchId).subscribe({
      next: () => { this.confirming = null; this.loadMatches(); },
      error: err => { this.confirming = null; this.actionError = err?.error?.error ?? 'Errore durante la conferma.'; },
    });
  }

  dispute(matchId: string): void {
    this.disputing = matchId;
    this.actionError = '';
    this.matchSvc.dispute(this.circleId, matchId).subscribe({
      next: () => { this.disputing = null; this.loadMatches(); },
      error: err => { this.disputing = null; this.actionError = err?.error?.error ?? 'Errore durante la contestazione.'; },
    });
  }

  forceConfirm(matchId: string): void {
    this.forceConfirming = matchId;
    this.actionError = '';
    this.matchSvc.forceConfirm(this.circleId, matchId).subscribe({
      next: () => { this.forceConfirming = null; this.loadMatches(); },
      error: err => { this.forceConfirming = null; this.actionError = err?.error?.error ?? 'Errore durante la conferma forzata.'; },
    });
  }
}
