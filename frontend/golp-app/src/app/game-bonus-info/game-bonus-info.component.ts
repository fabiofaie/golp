import { Component, signal } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Location } from '@angular/common';
import { GameBonusInfoService, SimulateGameBonusResponse } from './game-bonus-info.service';

@Component({
  selector: 'app-game-bonus-info',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './game-bonus-info.component.html',
  styleUrl: './game-bonus-info.component.scss'
})
export class GameBonusInfoComponent {
  form: FormGroup;
  resultMode: 'unico' | 'set' = 'unico';
  result = signal<SimulateGameBonusResponse | null>(null);
  loading = signal(false);
  errorMsg = signal('');

  constructor(private fb: FormBuilder, private gbService: GameBonusInfoService, private location: Location) {
    this.form = this.fb.group({
      team1CurrentScore: [0, [Validators.required, Validators.min(0)]],
      team2CurrentScore: [0, [Validators.required, Validators.min(0)]],
      myScore:  [null, [Validators.min(0)]],
      oppScore: [null, [Validators.min(0)]],
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

  isValid(): boolean {
    const scoresValid = !this.form.get('team1CurrentScore')?.invalid && !this.form.get('team2CurrentScore')?.invalid;
    if (!scoresValid) return false;
    return this.resultMode === 'unico'
      ? !this.form.get('myScore')?.invalid && !this.form.get('oppScore')?.invalid
      : !this.sets.invalid;
  }

  submit(): void {
    if (!this.isValid()) return;
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

    // Validazione UX per evitare la roundtrip HTTP: il backend resta la fonte di verità
    // e applica la stessa regola (vedi SimulateGameBonusEndpoints.SimulateAsync).
    const setsWonByMe  = setsPayload.filter(s => s.team1Score > s.team2Score).length;
    const setsWonByOpp = setsPayload.filter(s => s.team2Score > s.team1Score).length;
    if (setsWonByMe === setsWonByOpp) {
      this.errorMsg.set('Set vinti pari: deve esserci una squadra vincente.');
      return;
    }

    this.errorMsg.set('');
    this.loading.set(true);
    this.gbService.simulate({
      sets: setsPayload,
      team1CurrentScore: Number(v.team1CurrentScore ?? 0),
      team2CurrentScore: Number(v.team2CurrentScore ?? 0)
    }).subscribe({
      next: res => { this.result.set(res); this.loading.set(false); },
      error: () => { this.errorMsg.set('Errore nel calcolo. Riprova.'); this.loading.set(false); }
    });
  }

  goBack(): void { this.location.back(); }
}
