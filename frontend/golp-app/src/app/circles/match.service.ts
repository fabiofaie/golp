import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface SetScore {
  team1: number;
  team2: number;
}

export interface PlayerSlotDto {
  userId?: string;
  guestName?: string;
  guestEmail?: string;
  guestPhone?: string;
}

export interface CreateMatchRequest {
  team1: PlayerSlotDto[];
  team2: PlayerSlotDto[];
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
  isActivated?: boolean;
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

export interface PlayerDelta {
  userId: string;
  delta: number | null;
}

export interface MatchDetail extends MatchSummary {
  isParticipant: boolean;
  sets: MatchSetScore[];
  confirmedAt: string | null;
  confirmedByName: string | null;
  isForced: boolean | null;
  deltas: PlayerDelta[] | null;
}

export interface ConfirmDisputeResponse {
  status: string;
  confirmationsCount?: number;
}

// ── Public token API (US-040) ──────────────────────────────────────────────

export interface PublicMatchData {
  id: string;
  sport: string;
  circleName: string;
  status: 'pending' | 'confirmed' | 'disputed';
  winnerTeam: number;
  confirmationsCount: number;
  sets: { team1Score: number; team2Score: number }[];
  team1: PlayerInfo[];
  team2: PlayerInfo[];
}

export interface PublicMatchTokenInfo {
  valid?: boolean;
  userId: string;
  userName: string;
  userHasConfirmed?: boolean;
}

export interface PublicMatchResponse {
  tokenUsed: boolean;
  match: PublicMatchData;
  token: PublicMatchTokenInfo;
}

export interface PublicConfirmDisputeResponse {
  alreadyDone?: boolean;
  status: string;
  confirmationsCount?: number;
  isActivated?: boolean;
}

@Injectable({ providedIn: 'root' })
export class MatchService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  createMatch(circleId: string, body: CreateMatchRequest): Observable<MatchCreated> {
    return this.http.post<MatchCreated>(`${this.base}/circles/${circleId}/matches`, body);
  }

  getMatches(circleId: string): Observable<MatchSummary[]> {
    return this.http.get<MatchSummary[]>(`${this.base}/circles/${circleId}/matches`);
  }

  getMatchDetail(circleId: string, matchId: string): Observable<MatchDetail> {
    return this.http.get<MatchDetail>(`${this.base}/circles/${circleId}/matches/${matchId}`);
  }

  confirm(circleId: string, matchId: string): Observable<ConfirmDisputeResponse> {
    return this.http.post<ConfirmDisputeResponse>(`${this.base}/circles/${circleId}/matches/${matchId}/confirm`, null);
  }

  dispute(circleId: string, matchId: string): Observable<ConfirmDisputeResponse> {
    return this.http.post<ConfirmDisputeResponse>(`${this.base}/circles/${circleId}/matches/${matchId}/dispute`, null);
  }

  forceConfirm(circleId: string, matchId: string): Observable<ConfirmDisputeResponse> {
    return this.http.post<ConfirmDisputeResponse>(`${this.base}/circles/${circleId}/matches/${matchId}/force-confirm`, null);
  }

  getPublicMatch(token: string): Observable<PublicMatchResponse> {
    return this.http.get<PublicMatchResponse>(`${this.base}/m/${token}`);
  }

  confirmViaToken(token: string): Observable<PublicConfirmDisputeResponse> {
    return this.http.post<PublicConfirmDisputeResponse>(`${this.base}/m/${token}/confirm`, null);
  }

  disputeViaToken(token: string): Observable<PublicConfirmDisputeResponse> {
    return this.http.post<PublicConfirmDisputeResponse>(`${this.base}/m/${token}/dispute`, null);
  }
}
