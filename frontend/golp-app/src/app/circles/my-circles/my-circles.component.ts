import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { CircleService, CircleSummary } from '../circle.service';
import { AuthService } from '../../auth/auth.service';
import { InviteDialogComponent } from '../invite-dialog/invite-dialog.component';
import { AddMemberDialogComponent } from '../add-member-dialog/add-member-dialog.component';
import { CircleRatingConfigComponent } from '../circle-rating-config/circle-rating-config.component';

@Component({
  selector: 'app-my-circles',
  standalone: true,
  imports: [CommonModule, RouterLink, InviteDialogComponent, AddMemberDialogComponent, CircleRatingConfigComponent],
  templateUrl: './my-circles.component.html',
})
export class MyCirclesComponent implements OnInit {
  private readonly svc = inject(CircleService);
  private readonly authSvc = inject(AuthService);

  circles: CircleSummary[] = [];
  loading = true;
  errorMessage = '';
  currentUserId = this.authSvc.getCurrentUserId() ?? '';
  activeInviteCircle: CircleSummary | null = null;
  activeAddMemberCircle: CircleSummary | null = null;
  activeRatingConfigCircle: CircleSummary | null = null;

  ngOnInit(): void {
    this.svc.getMyCircles().subscribe({
      next: list => { this.circles = list; this.loading = false; },
      error: () => { this.errorMessage = 'Impossibile caricare i circoli.'; this.loading = false; },
    });
  }

  sportLabel(sport: string): string {
    const map: Record<string, string> = {
      padel: 'Padel',
      beachtennis: 'Beach Tennis',
      basket2v2: 'Basket 2v2',
      burraco: 'Burraco',
    };
    return map[sport] ?? sport;
  }

  sportClass(sport: string): string {
    return `sport-badge sport-badge--${sport}`;
  }

  openInvite(c: CircleSummary): void {
    this.activeInviteCircle = c;
  }

  closeInvite(): void {
    this.activeInviteCircle = null;
  }

  openAddMember(c: CircleSummary): void {
    this.activeAddMemberCircle = c;
  }

  closeAddMember(): void {
    this.activeAddMemberCircle = null;
  }

  openRatingConfig(c: CircleSummary): void {
    this.activeRatingConfigCircle = c;
  }

  closeRatingConfig(): void {
    this.activeRatingConfigCircle = null;
  }

  onRatingConfigSaved(res: { ratingMethod: 'Elo' | 'GameBonus'; gameBonusWindowMatches: number; gameBonusWindowWeeks: number }): void {
    if (this.activeRatingConfigCircle) {
      this.activeRatingConfigCircle.ratingMethod = res.ratingMethod;
      this.activeRatingConfigCircle.gameBonusWindowMatches = res.gameBonusWindowMatches;
      this.activeRatingConfigCircle.gameBonusWindowWeeks = res.gameBonusWindowWeeks;
    }
  }
}
