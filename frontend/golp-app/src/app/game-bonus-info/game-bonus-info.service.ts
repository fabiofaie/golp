import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface SimulateGameBonusSet {
  team1Score: number;
  team2Score: number;
}

export interface SimulateGameBonusRequest {
  sets: SimulateGameBonusSet[];
  team1CurrentScore: number;
  team2CurrentScore: number;
}

export interface SimulateGameBonusResponse {
  team1Points: number;
  team2Points: number;
}

@Injectable({ providedIn: 'root' })
export class GameBonusInfoService {
  constructor(private http: HttpClient) {}

  simulate(request: SimulateGameBonusRequest): Observable<SimulateGameBonusResponse> {
    return this.http.post<SimulateGameBonusResponse>(`${environment.apiUrl}/api/simulate-game-bonus`, request);
  }
}
