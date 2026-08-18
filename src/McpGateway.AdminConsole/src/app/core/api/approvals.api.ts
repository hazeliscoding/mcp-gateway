import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApprovalResponse, DecisionRequest } from '../models/approval';
import { ApprovalStatus } from '../models/enums';

@Injectable({ providedIn: 'root' })
export class ApprovalsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api/approvals`;

  list(status?: ApprovalStatus): Observable<ApprovalResponse[]> {
    let params = new HttpParams();
    if (status) {
      params = params.set('status', status);
    }
    return this.http.get<ApprovalResponse[]>(this.base, { params });
  }

  approve(id: string, note?: string): Observable<ApprovalResponse> {
    return this.decide(id, 'approve', note);
  }

  reject(id: string, note?: string): Observable<ApprovalResponse> {
    return this.decide(id, 'reject', note);
  }

  private decide(id: string, action: 'approve' | 'reject', note?: string): Observable<ApprovalResponse> {
    const body: DecisionRequest = { note };
    return this.http.post<ApprovalResponse>(`${this.base}/${encodeURIComponent(id)}/${action}`, body);
  }
}
