import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CircleService, MemberSummary } from '../circle.service';
import { AttendanceService } from '../attendance.service';
import { MatchmakingService, MatchmakingPlanDto, MatchmakingTargetMode, PlannedMatchDto } from '../matchmaking.service';

interface MemberRow extends MemberSummary {
  present: boolean;
}

@Component({
  selector: 'app-circle-gathering',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './circle-gathering.component.html',
  styleUrl: './circle-gathering.component.scss',
})
export class CircleGatheringComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly circleSvc = inject(CircleService);
  private readonly attendanceSvc = inject(AttendanceService);
  private readonly matchmakingSvc = inject(MatchmakingService);

  circleId = '';
  members: MemberRow[] = [];
  loading = true;
  errorMessage = '';

  courts = 1;
  targetMode: MatchmakingTargetMode = 'Total';
  targetValue = 4;

  plan: MatchmakingPlanDto | null = null;
  activeRoundIndex = 0;
  planLoading = false;
  planError = '';
  selectedPlayer: { matchIndex: number; team: 1 | 2; userId: string } | null = null;

  get presentCount(): number {
    return this.members.filter(m => m.present).length;
  }

  get canGeneratePlan(): boolean {
    return this.presentCount >= 4;
  }

  get activeRound() {
    return this.plan?.rounds[this.activeRoundIndex] ?? null;
  }

  ngOnInit(): void {
    this.circleId = this.route.snapshot.paramMap.get('circleId') ?? '';
    this.circleSvc.getMembers(this.circleId).subscribe({
      next: members => {
        this.members = members.map(m => ({ ...m, present: false }));
        this.loading = false;
      },
      error: () => {
        this.errorMessage = 'Impossibile caricare i membri del circolo.';
        this.loading = false;
      },
    });
  }

  toggleTargetMode(mode: MatchmakingTargetMode): void {
    this.targetMode = mode;
    this.targetValue = mode === 'Total' ? 4 : 2;
  }

  adjustCourts(delta: number): void {
    this.courts = Math.max(1, Math.min(8, this.courts + delta));
  }

  adjustTargetValue(delta: number): void {
    const max = this.targetMode === 'Total' ? 40 : 10;
    this.targetValue = Math.max(1, Math.min(max, this.targetValue + delta));
  }

  toggleMemberPresence(member: MemberRow): void {
    this.attendanceSvc.setAttendance(this.circleId, !member.present, member.userId).subscribe({
      next: () => { member.present = !member.present; },
      error: () => { this.errorMessage = 'Impossibile aggiornare la presenza. Riprova.'; },
    });
  }

  generatePlan(): void {
    if (!this.canGeneratePlan) return;
    this.planLoading = true;
    this.planError = '';
    this.matchmakingSvc.getSuggestion(this.circleId, this.courts, this.targetMode, this.targetValue).subscribe({
      next: plan => {
        this.plan = plan;
        this.activeRoundIndex = 0;
        this.planLoading = false;
      },
      error: () => {
        this.planError = 'Impossibile generare il piano. Verifica di avere almeno 4 presenti.';
        this.planLoading = false;
      },
    });
  }

  selectRound(index: number): void {
    this.activeRoundIndex = index;
    this.selectedPlayer = null;
  }

  memberName(userId: string): string {
    return this.members.find(m => m.userId === userId)?.name ?? 'Ospite';
  }

  // Iniziale nome + iniziale cognome (es. "Mario Rossi" -> "MR"); con un solo nome, prime 2 lettere.
  initials(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return (parts[0] ?? '').slice(0, 2).toUpperCase();
  }

  onPlayerClick(matchIndex: number, team: 1 | 2, userId: string): void {
    if (!this.selectedPlayer) {
      this.selectedPlayer = { matchIndex, team, userId };
      return;
    }
    if (this.selectedPlayer.matchIndex !== matchIndex) {
      this.selectedPlayer = { matchIndex, team, userId };
      return;
    }
    if (this.selectedPlayer.userId === userId) {
      this.selectedPlayer = null;
      return;
    }

    this.swapPlayers(matchIndex, this.selectedPlayer, { team, userId });
    this.selectedPlayer = null;
  }

  isSelected(matchIndex: number, userId: string): boolean {
    return this.selectedPlayer?.matchIndex === matchIndex && this.selectedPlayer?.userId === userId;
  }

  private swapPlayers(
    matchIndex: number,
    a: { team: 1 | 2; userId: string },
    b: { team: 1 | 2; userId: string },
  ): void {
    const round = this.activeRound;
    if (!round) return;
    const match = round.matches[matchIndex];
    const teamAKey: keyof PlannedMatchDto = a.team === 1 ? 'team1' : 'team2';
    const teamBKey: keyof PlannedMatchDto = b.team === 1 ? 'team1' : 'team2';
    const teamA = [...match[teamAKey]];
    const teamB = [...match[teamBKey]];
    const idxA = teamA.indexOf(a.userId);
    const idxB = teamB.indexOf(b.userId);
    if (idxA === -1 || idxB === -1) return;

    [teamA[idxA], teamB[idxB]] = [teamB[idxB], teamA[idxA]];
    match[teamAKey] = teamA;
    match[teamBKey] = teamB;
  }

  registerMatch(match: PlannedMatchDto): void {
    this.router.navigate(['/match/quick'], {
      queryParams: {
        circleId: this.circleId,
        team1p1: match.team1[0],
        team1p2: match.team1[1],
        team2p1: match.team2[0],
        team2p2: match.team2[1],
      },
    });
  }
}
