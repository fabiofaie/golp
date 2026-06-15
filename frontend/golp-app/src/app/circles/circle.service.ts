import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface SportConfig {
  sport: string;
  pointUnit: string;
  sets: boolean;
  teamSize: number;
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
}

export interface JoinResult {
  circleId: string;
  myRating: number;
}

export interface LeaderboardEntry {
  userId: string;
  name: string;
  rating: number;
  rank: number;
  confirmedMatches: number;
}

export interface LeaderboardUnclassified {
  userId: string;
  name: string;
}

export interface LeaderboardResponse {
  classified: LeaderboardEntry[];
  unclassified: LeaderboardUnclassified[];
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
}

export interface InviteLinkResponse {
  inviteToken: string;
}

export interface JoinByTokenResult {
  circleId: string;
  myRating: number;
  alreadyMember: boolean;
}

@Injectable({ providedIn: 'root' })
export class CircleService {
  private readonly http = inject(HttpClient);

  getSports(): Observable<SportConfig[]> {
    return this.http.get<SportConfig[]>('/sports');
  }

  getMyCircles(): Observable<CircleSummary[]> {
    return this.http.get<CircleSummary[]>('/circles/me');
  }

  getCircles(): Observable<CircleListItem[]> {
    return this.http.get<CircleListItem[]>('/circles');
  }

  joinCircle(id: string): Observable<JoinResult> {
    return this.http.post<JoinResult>(`/circles/${id}/join`, null);
  }

  getMembers(id: string): Observable<MemberSummary[]> {
    return this.http.get<MemberSummary[]>(`/circles/${id}/members`);
  }

  createCircle(name: string, sport: string): Observable<CircleCreated> {
    return this.http.post<CircleCreated>('/circles', { name, sport });
  }

  getLeaderboard(circleId: string): Observable<LeaderboardResponse> {
    return this.http.get<LeaderboardResponse>(`/circles/${circleId}/leaderboard`);
  }

  getAwards(circleId: string): Observable<CircleAwardsResponse> {
    return this.http.get<CircleAwardsResponse>(`/circles/${circleId}/awards`);
  }

  getMyStats(circleId: string): Observable<CircleStatsResponse> {
    return this.http.get<CircleStatsResponse>(`/circles/${circleId}/stats/me`);
  }

  getInviteLink(circleId: string): Observable<InviteLinkResponse> {
    return this.http.get<InviteLinkResponse>(`/circles/${circleId}/invite-link`);
  }

  joinByToken(inviteToken: string): Observable<JoinByTokenResult> {
    return this.http.post<JoinByTokenResult>('/circles/join-by-token', { inviteToken });
  }
}
