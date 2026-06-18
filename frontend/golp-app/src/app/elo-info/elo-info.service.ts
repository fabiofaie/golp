import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface SimulateTeam {
  player1Rating: number;
  player2Rating: number;
}

export interface SimulateSet {
  team1Score: number;
  team2Score: number;
}

export interface SimulateMatchRequest {
  team1: SimulateTeam;
  team2: SimulateTeam;
  sets: SimulateSet[];
  experienced: boolean;
}

export interface SimulateMatchResponse {
  deltaTeam1Player1: number;
  deltaTeam1Player2: number;
  deltaTeam2Player1: number;
  deltaTeam2Player2: number;
}

@Injectable({ providedIn: 'root' })
export class EloInfoService {
  constructor(private http: HttpClient) {}

  simulate(request: SimulateMatchRequest): Observable<SimulateMatchResponse> {
    return this.http.post<SimulateMatchResponse>(`${environment.apiUrl}/api/simulate-match`, request);
  }
}
