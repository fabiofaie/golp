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
}
