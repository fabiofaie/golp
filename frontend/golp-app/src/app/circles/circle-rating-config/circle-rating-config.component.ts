import { Component, Input, Output, EventEmitter, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { inject } from '@angular/core';
import { CircleService } from '../circle.service';

@Component({
  selector: 'app-circle-rating-config',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './circle-rating-config.component.html',
  styleUrl: './circle-rating-config.component.scss',
})
export class CircleRatingConfigComponent implements OnInit {
  @Input() circleId = '';
  @Input() circleName = '';
  @Input() ratingMethod: 'Elo' | 'GameBonus' = 'Elo';
  @Input() gameBonusWindowMatches = 30;
  @Input() gameBonusWindowWeeks = 6;
  @Output() closed = new EventEmitter<void>();
  @Output() saved = new EventEmitter<{ ratingMethod: 'Elo' | 'GameBonus'; gameBonusWindowMatches: number; gameBonusWindowWeeks: number }>();

  private readonly circleService = inject(CircleService);

  // Inizializzati in ngOnInit, non come field initializer: @Input() non è ancora valorizzato
  // al momento della costruzione del componente (Angular li assegna dopo il costruttore).
  selectedMethod: 'Elo' | 'GameBonus' = 'Elo';
  windowMatches = 30;
  windowWeeks = 6;
  saving = false;
  saved_ = false;
  error = '';

  ngOnInit(): void {
    this.selectedMethod = this.ratingMethod;
    this.windowMatches = this.gameBonusWindowMatches;
    this.windowWeeks = this.gameBonusWindowWeeks;
  }

  select(method: 'Elo' | 'GameBonus'): void {
    this.selectedMethod = method;
  }

  save(): void {
    this.saving = true;
    this.error = '';
    this.circleService
      .updateRatingConfig(this.circleId, this.selectedMethod, this.windowMatches, this.windowWeeks)
      .subscribe({
        next: (res) => {
          this.saving = false;
          this.saved_ = true;
          this.saved.emit(res);
          setTimeout(() => (this.saved_ = false), 1800);
        },
        error: () => {
          this.saving = false;
          this.error = 'Impossibile salvare la configurazione. Riprova.';
        },
      });
  }

  close(): void {
    this.closed.emit();
  }
}
