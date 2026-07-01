import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { MatchService, PublicMatchData, PublicMatchTokenInfo } from '../../circles/match.service';
import { HttpErrorResponse } from '@angular/common/http';

type PageState = 'loading' | 'ready' | 'token-expired' | 'token-not-found' | 'acting' | 'done-confirm' | 'done-dispute' | 'error';

const SPORT_ICONS: Record<string, string> = {
  padel: '🎾',
  beachtennis: '🏖️',
  basket2v2: '🏀',
  burraco: '🃏',
};

@Component({
  selector: 'app-match-public-confirm',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './match-public-confirm.component.html',
})
export class MatchPublicConfirmComponent implements OnInit {
  private readonly route    = inject(ActivatedRoute);
  private readonly matchSvc = inject(MatchService);

  token = '';
  state: PageState = 'loading';
  match: PublicMatchData | null = null;
  tokenInfo: PublicMatchTokenInfo | null = null;
  tokenUsed = false;
  isActivated = true;
  errorMessage = '';

  ngOnInit(): void {
    this.token = this.route.snapshot.paramMap.get('token') ?? '';
    this.load();
  }

  private load(): void {
    this.matchSvc.getPublicMatch(this.token).subscribe({
      next: res => {
        this.match = res.match;
        this.tokenInfo = res.token;
        this.tokenUsed = res.tokenUsed;
        this.state = 'ready';
      },
      error: (err: HttpErrorResponse) => {
        if (err.status === 410) { this.state = 'token-expired'; return; }
        if (err.status === 404) { this.state = 'token-not-found'; return; }
        this.state = 'error';
        this.errorMessage = 'Errore nel caricamento della partita.';
      },
    });
  }

  get canAct(): boolean {
    return (
      !this.tokenUsed &&
      !!this.tokenInfo?.valid &&
      !this.tokenInfo.userHasConfirmed &&
      this.match?.status === 'pending'
    );
  }

  confirm(): void {
    if (!this.canAct || this.state === 'acting') return;
    this.state = 'acting';
    this.matchSvc.confirmViaToken(this.token).subscribe({
      next: res => {
        if (this.match) this.match = { ...this.match, status: res.status as any, confirmationsCount: res.confirmationsCount ?? this.match.confirmationsCount };
        this.isActivated = res.isActivated ?? true;
        this.state = 'done-confirm';
      },
      error: () => { this.state = 'ready'; },
    });
  }

  dispute(): void {
    if (!this.canAct || this.state === 'acting') return;
    this.state = 'acting';
    this.matchSvc.disputeViaToken(this.token).subscribe({
      next: res => {
        if (this.match) this.match = { ...this.match, status: 'disputed' };
        this.isActivated = res.isActivated ?? true;
        this.state = 'done-dispute';
      },
      error: () => { this.state = 'ready'; },
    });
  }

  sportIcon(sport: string): string {
    return SPORT_ICONS[sport?.toLowerCase()] ?? '🏅';
  }

  scoreLabel(): string {
    if (!this.match?.sets?.length) return '';
    return this.match.sets.map(s => `${s.team1Score}–${s.team2Score}`).join('  ·  ');
  }

  team1Sets(): number {
    if (!this.match) return 0;
    return this.match.sets.filter(s => s.team1Score > s.team2Score).length;
  }

  team2Sets(): number {
    if (!this.match) return 0;
    return this.match.sets.filter(s => s.team2Score > s.team1Score).length;
  }
}
