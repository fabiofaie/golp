import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CircleService, MemberSummary, CircleSummary } from '../circle.service';
import { MatchService } from '../match.service';

interface SetRow {
  team1: number | null;
  team2: number | null;
}

@Component({
  selector: 'app-record-match',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './record-match.component.html',
})
export class RecordMatchComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly circleSvc = inject(CircleService);
  private readonly matchSvc = inject(MatchService);

  circleId = '';
  circle: CircleSummary | null = null;
  members: MemberSummary[] = [];

  team1Player1 = '';
  team1Player2 = '';
  team2Player1 = '';
  team2Player2 = '';

  sets: SetRow[] = [{ team1: null, team2: null }];
  singleTeam1: number | null = null;
  singleTeam2: number | null = null;

  loading = false;
  errorMessage = '';

  get useSets(): boolean {
    return this.circle?.sets ?? true;
  }

  ngOnInit(): void {
    this.circleId = this.route.snapshot.paramMap.get('circleId') ?? '';

    this.circleSvc.getMyCircles().subscribe({
      next: list => {
        this.circle = list.find(c => c.id === this.circleId) ?? null;
        if (!this.circle) {
          this.errorMessage = 'Circolo non trovato o non sei membro.';
        }
      },
    });

    this.circleSvc.getMembers(this.circleId).subscribe({
      next: m => { this.members = m; },
      error: () => { this.errorMessage = 'Impossibile caricare i membri del circolo.'; },
    });
  }

  addSet(): void {
    this.sets = [...this.sets, { team1: null, team2: null }];
  }

  removeSet(index: number): void {
    if (this.sets.length <= 1) return;
    this.sets = this.sets.filter((_, i) => i !== index);
  }

  updateSet(index: number, field: 'team1' | 'team2', value: number | null): void {
    this.sets = this.sets.map((s, i) =>
      i === index ? { ...s, [field]: value } : s
    );
  }

  submit(): void {
    this.errorMessage = '';

    if (!this.team1Player1 || !this.team1Player2 || !this.team2Player1 || !this.team2Player2) {
      this.errorMessage = 'Seleziona tutti e 4 i giocatori.';
      return;
    }

    const team1 = [this.team1Player1, this.team1Player2];
    const team2 = [this.team2Player1, this.team2Player2];

    const setsPayload = this.useSets
      ? this.sets.map(s => ({ team1: s.team1 ?? 0, team2: s.team2 ?? 0 }))
      : [{ team1: this.singleTeam1 ?? 0, team2: this.singleTeam2 ?? 0 }];

    this.loading = true;
    this.matchSvc.createMatch(this.circleId, { team1, team2, sets: setsPayload }).subscribe({
      next: () => {
        this.loading = false;
        this.router.navigate(['/circles']);
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.error?.error ?? 'Errore durante l\'inserimento della partita.';
      },
    });
  }

  memberName(id: string): string {
    return this.members.find(m => m.userId === id)?.name ?? id;
  }

  availableForSlot(excludeIds: string[]): MemberSummary[] {
    return this.members.filter(m => !excludeIds.includes(m.userId));
  }
}
