import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { CircleService, CircleListItem } from '../circle.service';

@Component({
  selector: 'app-browse-circles',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './browse-circles.component.html',
})
export class BrowseCirclesComponent implements OnInit {
  private readonly svc    = inject(CircleService);
  private readonly router = inject(Router);

  circles: CircleListItem[] = [];
  loading      = true;
  errorMessage = '';
  joiningId: string | null = null;

  ngOnInit(): void {
    this.svc.getCircles().subscribe({
      next: list => { this.circles = list; this.loading = false; },
      error: () => { this.errorMessage = 'Impossibile caricare i circoli.'; this.loading = false; },
    });
  }

  join(id: string): void {
    if (this.joiningId) return;
    this.joiningId = id;

    this.svc.joinCircle(id).subscribe({
      next: () => {
        this.circles = this.circles.map(c =>
          c.id === id ? { ...c, isAlreadyMember: true } : c
        );
        this.joiningId = null;
      },
      error: err => {
        this.joiningId = null;
        if (err.status === 409) {
          this.circles = this.circles.map(c =>
            c.id === id ? { ...c, isAlreadyMember: true } : c
          );
        } else {
          this.errorMessage = err.error?.error ?? 'Errore durante l\'iscrizione.';
        }
      },
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
