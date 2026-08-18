import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  RegisterToolRequest,
  RegisterVersionRequest,
  ToolDetailResponse,
  ToolListFilter,
  ToolSummaryResponse,
} from '../models/tool';

@Injectable({ providedIn: 'root' })
export class ToolsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api/tools`;

  list(filter: ToolListFilter = {}): Observable<ToolSummaryResponse[]> {
    let params = new HttpParams();
    if (filter.riskLevel) {
      params = params.set('riskLevel', filter.riskLevel);
    }
    if (filter.includeDisabled) {
      params = params.set('includeDisabled', true);
    }
    if (filter.nameContains) {
      params = params.set('nameContains', filter.nameContains);
    }
    return this.http.get<ToolSummaryResponse[]>(this.base, { params });
  }

  get(name: string): Observable<ToolDetailResponse> {
    return this.http.get<ToolDetailResponse>(`${this.base}/${encodeURIComponent(name)}`);
  }

  register(request: RegisterToolRequest): Observable<ToolDetailResponse> {
    return this.http.post<ToolDetailResponse>(this.base, request);
  }

  addVersion(name: string, request: RegisterVersionRequest): Observable<ToolDetailResponse> {
    return this.http.post<ToolDetailResponse>(`${this.base}/${encodeURIComponent(name)}/versions`, request);
  }

  setEnabled(name: string, enabled: boolean): Observable<void> {
    const action = enabled ? 'enable' : 'disable';
    return this.http.post<void>(`${this.base}/${encodeURIComponent(name)}/${action}`, {});
  }

  deprecateVersion(name: string, version: string): Observable<void> {
    return this.http.post<void>(
      `${this.base}/${encodeURIComponent(name)}/versions/${encodeURIComponent(version)}/deprecate`,
      {},
    );
  }
}
