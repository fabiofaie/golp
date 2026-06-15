import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface SetScore {
  team1: number;
  team2: number;
}

export interface CreateMatchRequest {
  team1: string[];
  team2: string[];
  sets: SetScore[];
}

export interface MatchCreated {
  id: string;
  circleId: string;
  status: string;
  winnerTeam: number;
  createdAt: string;
}

export interface PlayerInfo {
  userId: string;
  name: string;
}

export interface MatchSummary {
  id: string;
  status: 'pending' | 'confirmed' | 'disputed';
  winnerTeam: number;
  createdAt: string;
  myDelta: number | null;
  confirmationsCount: number;
  hasCurrentUserConfirmed: boolean;
  team1: PlayerInfo[];
  team2: PlayerInfo[];
}

export interface MatchSetScore {
  team1Score: number;
  team2Score: number;
}

export interface MatchDetail extends MatchSummary {
  isParticipant: boolean;
  sets: MatchSetScore[];
}

export interface ConfirmDisputeResponse {
  status: string;
  confirmationsCount?: number;
}

@Injectable({ providedIn: 'root' })
export class MatchService {
  private readonly http = inject(HttpClient);

  createMatch(circleId: string, body: CreateMatchRequest): Observable<MatchCreated> {
    return this.http.post<MatchCreated>(`/circles/${circleId}/matches`, body);
  }

  getMatches(circleId: string): Observable<MatchSummary[]> {
    return this.http.get<MatchSummary[]>(`/circles/${circleId}/matches`);
  }

  getMatchDetail(circleId: string, matchId: string): Observable<MatchDetail> {
    return this.http.get<MatchDetail>(`/circles/${circleId}/matches/${matchId}`);
  }

  confirm(circleId: string, matchId: string): Observable<ConfirmDisputeResponse> {
    return this.http.post<ConfirmDisputeResponse>(`/circles/${circleId}/matches/${matchId}/confirm`, null);
  }

  dispute(circleId: string, matchId: string): Observable<ConfirmDisputeResponse> {
    return this.http.post<ConfirmDisputeResponse>(`/circles/${circleId}/matches/${matchId}/dispute`, null);
  }

  forceConfirm(circleId: string, matchId: string): Observable<ConfirmDisputeResponse> {
    return this.http.post<ConfirmDisputeResponse>(`/circles/${circleId}/matches/${matchId}/force-confirm`, null);
  }
}
