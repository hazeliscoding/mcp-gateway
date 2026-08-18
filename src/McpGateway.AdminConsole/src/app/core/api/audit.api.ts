import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuditEntryResponse, AuditQueryFilter, AuditStatsResponse } from '../models/audit';

@Injectable({ providedIn: 'root' })
export class AuditApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api/audit`;

  query(filter: AuditQueryFilter = {}): Observable<AuditEntryResponse[]> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filter)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<AuditEntryResponse[]>(this.base, { params });
  }

  stats(from?: string, to?: string): Observable<AuditStatsResponse> {
    let params = new HttpParams();
    if (from) {
      params = params.set('from', from);
    }
    if (to) {
      params = params.set('to', to);
    }
    return this.http.get<AuditStatsResponse>(`${this.base}/stats`, { params });
  }
}
