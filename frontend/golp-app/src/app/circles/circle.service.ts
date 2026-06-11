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

@Injectable({ providedIn: 'root' })
export class CircleService {
  private readonly http = inject(HttpClient);

  getSports(): Observable<SportConfig[]> {
    return this.http.get<SportConfig[]>('/sports');
  }

  getMyCircles(): Observable<CircleSummary[]> {
    return this.http.get<CircleSummary[]>('/circles/me');
  }

  createCircle(name: string, sport: string): Observable<CircleCreated> {
    return this.http.post<CircleCreated>('/circles', { name, sport });
  }
}
