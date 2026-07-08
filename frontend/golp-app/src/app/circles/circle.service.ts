import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface SportConfig {
  sport: string;
  displayName: string;
  pointUnit: string;
  sets: boolean;
  teamSize: number;
  allowsSingles: boolean;
}

export interface CircleSummary {
  id: string;
  name: string;
  sport: string;
  sets: boolean;
  pointUnit: string;
  ownerId: string;
  memberCount: number;
  myRating: number;
  myRank: number;
  ratingMethod?: 'Elo' | 'GameBonus';
  gameBonusWindowMatches?: number;
  gameBonusWindowWeeks?: number;
}

export interface CircleCreated {
  id: string;
  name: string;
  sport: string;
  pointUnit: string;
  sets: boolean;
  teamSize: number;
  joinCode: string | null;
  memberCount: number;
}

export interface CircleListItem {
  id: string;
  name: string;
  sport: string;
  memberCount: number;
  isAlreadyMember: boolean;
}

export interface MemberSummary {
  userId: string;
  name: string;
  rating: number;
  rank: number;
  isActivated?: boolean;
}

export interface JoinResult {
  circleId: string;
  myRating: number;
}

export interface LeaderboardEntry {
  userId: string;
  name: string;
  rating: number;
  gameBonusPoints?: number | null;
  rank: number;
  confirmedMatches: number;
  isActivated?: boolean;
}

export interface LeaderboardUnclassified {
  userId: string;
  name: string;
  isActivated?: boolean;
}

export interface LeaderboardResponse {
  classified: LeaderboardEntry[];
  unclassified: LeaderboardUnclassified[];
  ratingMethod?: 'Elo' | 'GameBonus';
}

export interface UpdateRatingConfigResult {
  ratingMethod: 'Elo' | 'GameBonus';
  gameBonusWindowMatches: number;
  gameBonusWindowWeeks: number;
}

export interface AwardWinner {
  userId: string;
  name: string;
  netGain: number;
  matchesPlayed: number;
}

export interface AwardPeriodResult {
  period: string;
  winner: AwardWinner | null;
}

export interface CircleAwardsResponse {
  currentMonth: AwardPeriodResult;
  currentYear: AwardPeriodResult;
}

export interface PlayerStatSummary {
  userId: string;
  name: string;
  winRate: number;
  gamesTogether?: number;
  gamesAgainst?: number;
}

export interface CircleStatsResponse {
  bestPartner: PlayerStatSummary | null;
  toughestOpponent: PlayerStatSummary | null;
  matchesWon: number;
  matchesLost: number;
  gamesWon: number;
  gamesLost: number;
  recentForm: string[];
}

export interface InviteLinkResponse {
  inviteToken: string;
}

export interface JoinByTokenResult {
  circleId: string;
  myRating: number;
  alreadyMember: boolean;
}

export interface InviteInfo {
  valid: boolean;
  circleName?: string;
}

@Injectable({ providedIn: 'root' })
export class CircleService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  getSports(): Observable<SportConfig[]> {
    return this.http.get<SportConfig[]>(`${this.base}/sports`);
  }

  getMyCircles(): Observable<CircleSummary[]> {
    return this.http.get<CircleSummary[]>(`${this.base}/circles/me`);
  }

  getCircles(): Observable<CircleListItem[]> {
    return this.http.get<CircleListItem[]>(`${this.base}/circles`);
  }

  joinCircle(id: string): Observable<JoinResult> {
    return this.http.post<JoinResult>(`${this.base}/circles/${id}/join`, null);
  }

  getMembers(id: string): Observable<MemberSummary[]> {
    return this.http.get<MemberSummary[]>(`${this.base}/circles/${id}/members`);
  }

  createCircle(name: string, sport: string): Observable<CircleCreated> {
    return this.http.post<CircleCreated>(`${this.base}/circles`, { name, sport });
  }

  getLeaderboard(circleId: string): Observable<LeaderboardResponse> {
    return this.http.get<LeaderboardResponse>(`${this.base}/circles/${circleId}/leaderboard`);
  }

  getAwards(circleId: string): Observable<CircleAwardsResponse> {
    return this.http.get<CircleAwardsResponse>(`${this.base}/circles/${circleId}/awards`);
  }

  getMyStats(circleId: string): Observable<CircleStatsResponse> {
    return this.http.get<CircleStatsResponse>(`${this.base}/circles/${circleId}/stats/me`);
  }

  getInviteLink(circleId: string): Observable<InviteLinkResponse> {
    return this.http.get<InviteLinkResponse>(`${this.base}/circles/${circleId}/invite-link`);
  }

  joinByToken(inviteToken: string): Observable<JoinByTokenResult> {
    return this.http.post<JoinByTokenResult>(`${this.base}/circles/join-by-token`, { inviteToken });
  }

  getInviteInfo(token: string): Observable<InviteInfo> {
    return this.http.get<InviteInfo>(`${this.base}/circles/invite/${token}`);
  }

  updateRatingConfig(
    circleId: string,
    ratingMethod: 'Elo' | 'GameBonus',
    gameBonusWindowMatches?: number,
    gameBonusWindowWeeks?: number,
  ): Observable<UpdateRatingConfigResult> {
    return this.http.put<UpdateRatingConfigResult>(`${this.base}/circles/${circleId}/rating-config`, {
      ratingMethod,
      gameBonusWindowMatches,
      gameBonusWindowWeeks,
    });
  }
}
