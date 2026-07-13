import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export type MatchmakingTargetMode = 'Total' | 'PerPlayer';

export interface PlannedMatchDto {
  team1: string[];
  team2: string[];
}

export interface PlannedRoundDto {
  index: number;
  matches: PlannedMatchDto[];
  resting: string[];
}

export interface MatchmakingPlanDto {
  rounds: PlannedRoundDto[];
}

@Injectable({ providedIn: 'root' })
export class MatchmakingService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  getSuggestion(
    circleId: string,
    courts: number,
    targetMode: MatchmakingTargetMode,
    targetValue: number,
  ): Observable<MatchmakingPlanDto> {
    return this.http.post<MatchmakingPlanDto>(`${this.base}/circles/${circleId}/matchmaking-suggestion`, {
      courts,
      targetMode,
      targetValue,
    });
  }
}
