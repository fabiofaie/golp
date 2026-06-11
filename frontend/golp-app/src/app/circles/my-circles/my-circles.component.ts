import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { CircleService, CircleSummary } from '../circle.service';

@Component({
  selector: 'app-my-circles',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './my-circles.component.html',
})
export class MyCirclesComponent implements OnInit {
  private readonly svc = inject(CircleService);

  circles: CircleSummary[] = [];
  loading = true;
  errorMessage = '';

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
}
