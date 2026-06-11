import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CircleService, SportConfig } from '../circle.service';

@Component({
  selector: 'app-create-circle',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './create-circle.component.html',
})
export class CreateCircleComponent implements OnInit {
  private readonly fb      = inject(FormBuilder);
  private readonly svc     = inject(CircleService);
  private readonly router  = inject(Router);

  form = this.fb.group({
    name:  ['', [Validators.required, Validators.maxLength(100)]],
    sport: ['', Validators.required],
  });

  sports: SportConfig[] = [];
  loading    = false;
  errorMessage = '';

  get selectedSport(): SportConfig | undefined {
    return this.sports.find(s => s.sport === this.form.value.sport);
  }

  ngOnInit(): void {
    this.svc.getSports().subscribe({
      next: s => this.sports = s,
      error: () => this.errorMessage = 'Impossibile caricare le discipline.',
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

  submit(): void {
    if (this.form.invalid || this.loading) return;
    this.loading = true;
    this.errorMessage = '';

    const { name, sport } = this.form.value;
    this.svc.createCircle(name!, sport!).subscribe({
      next: () => this.router.navigate(['/circles']),
      error: err => {
        this.loading = false;
        if (err.status === 409) {
          this.errorMessage = 'Hai già un circolo con questo nome.';
        } else {
          this.errorMessage = err.error?.error ?? 'Errore nella creazione del circolo.';
        }
      },
    });
  }
}
