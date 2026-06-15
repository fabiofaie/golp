import { Component, signal } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Location } from '@angular/common';
import { EloInfoService, SimulateMatchResponse } from './elo-info.service';

@Component({
  selector: 'app-elo-info',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './elo-info.component.html',
  styleUrl: './elo-info.component.scss'
})
export class EloInfoComponent {
  form: FormGroup;
  resultMode: 'unico' | 'set' = 'unico';
  playerType: 'esperto' | 'nuovo' = 'esperto';
  result = signal<SimulateMatchResponse | null>(null);
  loading = signal(false);
  errorMsg = signal('');

  constructor(private fb: FormBuilder, private eloService: EloInfoService, private location: Location) {
    this.form = this.fb.group({
      myRating:      [null, [Validators.required, Validators.min(0), Validators.max(3000)]],
      partnerRating: [null, [Validators.required, Validators.min(0), Validators.max(3000)]],
      opp1Rating:    [null, [Validators.required, Validators.min(0), Validators.max(3000)]],
      opp2Rating:    [null, [Validators.required, Validators.min(0), Validators.max(3000)]],
      myScore:       [null, [Validators.min(0)]],
      oppScore:      [null, [Validators.min(0)]],
      sets: this.fb.array([this.newSetRow()])
    });
  }

  get sets(): FormArray { return this.form.get('sets') as FormArray; }

  newSetRow(): FormGroup {
    return this.fb.group({
      myScore:  [null, [Validators.required, Validators.min(0)]],
      oppScore: [null, [Validators.required, Validators.min(0)]]
    });
  }

  addSet(): void { this.sets.push(this.newSetRow()); }

  removeSet(i: number): void {
    if (this.sets.length > 1) this.sets.removeAt(i);
  }

  setMode(mode: 'unico' | 'set'): void {
    this.resultMode = mode;
    this.result.set(null);
  }

  setPlayerType(type: 'esperto' | 'nuovo'): void {
    this.playerType = type;
    this.result.set(null);
  }

  playerTypeNote(): string {
    return this.playerType === 'esperto'
      ? 'Il tuo rating è già stabile — i punti vinti e persi variano moderatamente.'
      : 'Stai ancora calibrando il tuo livello — il rating sale e scende più velocemente nelle prime partite.';
  }

  submit(): void {
    if (this.form.invalid) return;
    const v = this.form.value;

    let setsPayload: { team1Score: number; team2Score: number }[];
    if (this.resultMode === 'unico') {
      const ms = Number(v.myScore ?? 0);
      const os = Number(v.oppScore ?? 0);
      if (ms + os === 0) { this.errorMsg.set('Inserisci almeno un punteggio maggiore di 0.'); return; }
      setsPayload = [{ team1Score: ms, team2Score: os }];
    } else {
      setsPayload = (v.sets as { myScore: number; oppScore: number }[]).map(s => ({
        team1Score: Number(s.myScore ?? 0),
        team2Score: Number(s.oppScore ?? 0)
      }));
      if (setsPayload.every(s => s.team1Score + s.team2Score === 0)) {
        this.errorMsg.set('Inserisci almeno un punteggio maggiore di 0.');
        return;
      }
    }

    this.errorMsg.set('');
    this.loading.set(true);
    this.eloService.simulate({
      team1: { player1Rating: Number(v.myRating), player2Rating: Number(v.partnerRating) },
      team2: { player1Rating: Number(v.opp1Rating), player2Rating: Number(v.opp2Rating) },
      sets: setsPayload,
      experienced: this.playerType === 'esperto'
    }).subscribe({
      next: res => { this.result.set(res); this.loading.set(false); },
      error: () => { this.errorMsg.set('Errore nel calcolo. Riprova.'); this.loading.set(false); }
    });
  }

  goBack(): void { this.location.back(); }

  formatDelta(d: number): string { return d >= 0 ? `+${d}` : `${d}`; }
  isPositive(d: number): boolean { return d >= 0; }
}
