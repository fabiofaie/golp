import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { CircleService, LeaderboardEntry, LeaderboardResponse, LeaderboardUnclassified } from '../circle.service';
import { AuthService } from '../../auth/auth.service';

@Component({
  selector: 'app-circle-leaderboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './circle-leaderboard.component.html',
  styleUrl: './circle-leaderboard.component.scss',
})
export class CircleLeaderboardComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly circleSvc = inject(CircleService);
  private readonly authSvc = inject(AuthService);

  circleId = '';
  currentUserId = '';
  loading = true;
  errorMessage = '';
  classified: LeaderboardEntry[] = [];
  unclassified: LeaderboardUnclassified[] = [];

  get top3(): LeaderboardEntry[] {
    return this.classified.slice(0, 3);
  }

  get podium1st(): LeaderboardEntry | undefined { return this.classified[0]; }
  get podium2nd(): LeaderboardEntry | undefined { return this.classified[1]; }
  get podium3rd(): LeaderboardEntry | undefined { return this.classified[2]; }

  ngOnInit(): void {
    this.circleId = this.route.snapshot.params['circleId'];
    this.currentUserId = this.authSvc.getCurrentUserId() ?? '';
    this.loadLeaderboard();
  }

  loadLeaderboard(): void {
    this.loading = true;
    this.errorMessage = '';
    this.circleSvc.getLeaderboard(this.circleId).subscribe({
      next: (data: LeaderboardResponse) => {
        this.classified = data.classified;
        this.unclassified = data.unclassified;
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'Errore nel caricamento della classifica.';
        this.loading = false;
      },
    });
  }

  isCurrentUser(userId: string): boolean {
    return userId === this.currentUserId;
  }

  trackById(_: number, entry: { userId: string }): string {
    return entry.userId;
  }
}
