import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { CircleService, MemberSummary, CircleSummary } from '../circle.service';
import { MatchService, MatchCreated, PlayerSlotDto } from '../match.service';
import { ShareConfirmComponent } from '../share-confirm/share-confirm.component';

interface PlayerSlot {
  mode: 'membro' | 'ospite';
  userId: string;
  guestName: string;
  guestEmail: string;
  guestPhone: string;
}

interface SetRow {
  team1: number | null;
  team2: number | null;
}

function emptySlot(): PlayerSlot {
  return { mode: 'membro', userId: '', guestName: '', guestEmail: '', guestPhone: '' };
}

@Component({
  selector: 'app-record-match',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, ShareConfirmComponent],
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
  allowsSingles = false;
  isSingles = false;

  slots: PlayerSlot[] = [emptySlot(), emptySlot(), emptySlot(), emptySlot()];

  sets: SetRow[] = [{ team1: null, team2: null }];
  singleTeam1: number | null = null;
  singleTeam2: number | null = null;

  loading = false;
  errorMessage = '';
  matchCreated: MatchCreated | null = null;

  readonly contactPickerAvailable: boolean =
    typeof navigator !== 'undefined' &&
    'contacts' in navigator &&
    'ContactsManager' in window;

  get useSets(): boolean {
    return this.circle?.sets ?? true;
  }

  get team1Slots(): number[] {
    return this.isSingles ? [0] : [0, 1];
  }

  get team2Slots(): number[] {
    return this.isSingles ? [2] : [2, 3];
  }

  get activeSlots(): number[] {
    return [...this.team1Slots, ...this.team2Slots];
  }

  toggleFormat(singles: boolean): void {
    this.isSingles = singles;
  }

  ngOnInit(): void {
    this.circleId = this.route.snapshot.paramMap.get('circleId') ?? '';

    forkJoin({
      circles: this.circleSvc.getMyCircles(),
      sports: this.circleSvc.getSports(),
    }).subscribe({
      next: ({ circles, sports }) => {
        this.circle = circles.find(c => c.id === this.circleId) ?? null;
        if (!this.circle) {
          this.errorMessage = 'Circolo non trovato o non sei membro.';
          return;
        }
        const sport = sports.find(s => s.sport === this.circle!.sport);
        this.allowsSingles = sport?.allowsSingles ?? false;
      },
    });

    this.circleSvc.getMembers(this.circleId).subscribe({
      next: m => { this.members = m; },
      error: () => { this.errorMessage = 'Impossibile caricare i membri del circolo.'; },
    });

    this.applyPrefillFromQueryParams();
  }

  // US-049: pre-fill dei 4 giocatori quando si arriva dal piano accoppiamenti del raduno
  // (circle-gathering). Nessun nuovo stato: riusa gli stessi slot della registrazione manuale.
  private applyPrefillFromQueryParams(): void {
    const qp = this.route.snapshot.queryParamMap;
    const prefill = [qp.get('team1p1'), qp.get('team1p2'), qp.get('team2p1'), qp.get('team2p2')];
    if (prefill.every(v => !v)) return;

    this.slots = this.slots.map((slot, i) =>
      prefill[i] ? { ...slot, mode: 'membro', userId: prefill[i] as string } : slot
    );
  }

  setSlotMode(index: number, mode: 'membro' | 'ospite'): void {
    const s = { ...this.slots[index], mode };
    if (mode === 'membro') {
      s.guestName = '';
      s.guestEmail = '';
      s.guestPhone = '';
    } else {
      s.userId = '';
    }
    this.slots = this.slots.map((sl, i) => (i === index ? s : sl));
  }

  updateSlotField(index: number, field: keyof PlayerSlot, value: string): void {
    this.slots = this.slots.map((sl, i) =>
      i === index ? { ...sl, [field]: value } : sl
    );
  }

  async pickContact(index: number): Promise<void> {
    if (!this.contactPickerAvailable) return;
    try {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const contacts: Array<{ name?: string[]; tel?: string[] }> = await (navigator as any).contacts.select(
        ['name', 'tel'],
        { multiple: false }
      );
      if (contacts.length === 0) return;
      const c = contacts[0];
      const name = c.name?.[0] ?? '';
      const tel = c.tel?.[0] ?? '';
      this.slots = this.slots.map((sl, i) =>
        i === index
          ? { ...sl, guestName: name, guestPhone: tel }
          : sl
      );
    } catch {
      // user dismissed picker — no-op
    }
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

  otherSlotIndexes(index: number): number[] {
    return this.activeSlots.filter(i => i !== index);
  }

  availableForSlot(excludeIndexes: number[]): MemberSummary[] {
    const usedIds = excludeIndexes
      .map(i => this.slots[i])
      .filter(s => s.mode === 'membro' && s.userId)
      .map(s => s.userId);
    return this.members.filter(m => !usedIds.includes(m.userId));
  }

  submit(): void {
    this.errorMessage = '';

    for (const i of this.activeSlots) {
      const s = this.slots[i];
      if (s.mode === 'membro' && !s.userId) {
        this.errorMessage = `Seleziona il giocatore ${i + 1} o scegli modalità nuovo giocatore.`;
        return;
      }
      if (s.mode === 'ospite') {
        if (!s.guestName.trim()) {
          this.errorMessage = `Inserisci il nome del nuovo giocatore ${i + 1}.`;
          return;
        }
        if (!s.guestEmail.trim() && !s.guestPhone.trim()) {
          this.errorMessage = `Inserisci email o telefono per il nuovo giocatore ${i + 1}.`;
          return;
        }
      }
    }

    const toDto = (s: PlayerSlot): PlayerSlotDto =>
      s.mode === 'membro'
        ? { userId: s.userId }
        : {
            guestName: s.guestName.trim(),
            guestEmail: s.guestEmail.trim() || undefined,
            guestPhone: s.guestPhone.trim() || undefined,
          };

    const team1 = this.team1Slots.map(i => toDto(this.slots[i]));
    const team2 = this.team2Slots.map(i => toDto(this.slots[i]));

    const setsPayload = this.useSets
      ? this.sets.map(s => ({ team1: s.team1 ?? 0, team2: s.team2 ?? 0 }))
      : [{ team1: this.singleTeam1 ?? 0, team2: this.singleTeam2 ?? 0 }];

    this.loading = true;
    this.matchSvc.createMatch(this.circleId, { team1, team2, sets: setsPayload, isSingles: this.isSingles }).subscribe({
      next: result => {
        this.loading = false;
        this.matchCreated = result;
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.error?.error ?? 'Errore durante l\'inserimento della partita.';
      },
    });
  }
}
