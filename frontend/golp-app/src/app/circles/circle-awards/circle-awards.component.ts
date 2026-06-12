import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CircleService, CircleAwardsResponse } from '../circle.service';

@Component({
  selector: 'app-circle-awards',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './circle-awards.component.html',
  styleUrl: './circle-awards.component.scss',
})
export class CircleAwardsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly circleService = inject(CircleService);

  circleId = '';
  loading = true;
  errorMessage = '';
  awards: CircleAwardsResponse | null = null;

  ngOnInit(): void {
    this.circleId = this.route.snapshot.params['circleId'];
    this.circleService.getAwards(this.circleId).subscribe({
      next: (data) => {
        this.awards = data;
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'Impossibile caricare i premi.';
        this.loading = false;
      },
    });
  }

  formatPeriod(period: string): string {
    const parts = period.split('-');
    if (parts.length === 2) {
      const months = ['Gen', 'Feb', 'Mar', 'Apr', 'Mag', 'Giu', 'Lug', 'Ago', 'Set', 'Ott', 'Nov', 'Dic'];
      const m = parseInt(parts[1], 10) - 1;
      return `${months[m]} ${parts[0]}`;
    }
    return period;
  }
}
