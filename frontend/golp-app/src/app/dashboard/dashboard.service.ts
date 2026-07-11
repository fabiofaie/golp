import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { CircleSummary } from '../circles/circle.service';
import { MatchSummary, MyMatchSummary } from '../circles/match.service';

export interface DashboardActiveCircle {
  id: string;
  name: string;
  sport: string;
  myRating: number;
  myRank: number;
  memberCount: number;
  confirmedMatchesCount: number;
  recentMatches: MatchSummary[];
}

export interface DashboardAggregate {
  circlesCount: number;
  confirmedMatchesCount: number;
  winRate: number;
}

export interface DashboardSummary {
  circles: CircleSummary[];
  activeCircle: DashboardActiveCircle | null;
  aggregate: DashboardAggregate | null;
  urgentMatches: MyMatchSummary[];
}

/** US-070: unica chiamata di rete per popolare l'intera dashboard, sostituisce le fetch separate. */
@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  getDashboardSummary(circleId?: string): Observable<DashboardSummary> {
    const query = circleId ? `?circleId=${encodeURIComponent(circleId)}` : '';
    return this.http.get<DashboardSummary>(`${this.base}/dashboard/summary${query}`);
  }
}
