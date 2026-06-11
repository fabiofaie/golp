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

@Injectable({ providedIn: 'root' })
export class MatchService {
  private readonly http = inject(HttpClient);

  createMatch(circleId: string, body: CreateMatchRequest): Observable<MatchCreated> {
    return this.http.post<MatchCreated>(`/circles/${circleId}/matches`, body);
  }
}
