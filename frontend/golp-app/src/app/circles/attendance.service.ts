import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface SetAttendanceResult {
  userId: string;
  present: boolean;
}

@Injectable({ providedIn: 'root' })
export class AttendanceService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  setAttendance(circleId: string, present: boolean, userId?: string): Observable<SetAttendanceResult> {
    return this.http.post<SetAttendanceResult>(`${this.base}/circles/${circleId}/attendance`, { present, userId });
  }
}
