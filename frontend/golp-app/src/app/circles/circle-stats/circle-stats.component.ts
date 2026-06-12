import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CircleService, CircleStatsResponse } from '../circle.service';

@Component({
  selector: 'app-circle-stats',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './circle-stats.component.html',
  styleUrl: './circle-stats.component.scss',
})
export class CircleStatsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly circleService = inject(CircleService);

  circleId = '';
  loading = true;
  errorMessage = '';
  stats: CircleStatsResponse | null = null;

  ngOnInit(): void {
    this.circleId = this.route.snapshot.params['circleId'];
    this.circleService.getMyStats(this.circleId).subscribe({
      next: (data) => {
        this.stats = data;
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'Impossibile caricare le statistiche.';
        this.loading = false;
      },
    });
  }

  get isEmpty(): boolean {
    return this.stats?.bestPartner === null && this.stats?.toughestOpponent === null;
  }

  winRatePct(rate: number): string {
    return Math.round(rate * 100).toString();
  }

  winRateCircumference = 2 * Math.PI * 44;

  partnerDashOffset(rate: number): number {
    return this.winRateCircumference * (1 - rate);
  }

  opponentDashOffset(rate: number): number {
    return this.winRateCircumference * (1 - rate);
  }
}
