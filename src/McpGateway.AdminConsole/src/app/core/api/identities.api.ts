import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { IdentityResponse, IssuedSecretResponse, RegisterIdentityRequest } from '../models/identity';

@Injectable({ providedIn: 'root' })
export class IdentitiesApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/api/identities`;

  list(): Observable<IdentityResponse[]> {
    return this.http.get<IdentityResponse[]>(this.base);
  }

  register(request: RegisterIdentityRequest): Observable<IssuedSecretResponse> {
    return this.http.post<IssuedSecretResponse>(this.base, request);
  }

  setEnabled(clientId: string, enabled: boolean): Observable<void> {
    const action = enabled ? 'enable' : 'disable';
    return this.http.post<void>(`${this.base}/${encodeURIComponent(clientId)}/${action}`, {});
  }

  rotateSecret(clientId: string): Observable<IssuedSecretResponse> {
    return this.http.post<IssuedSecretResponse>(`${this.base}/${encodeURIComponent(clientId)}/rotate-secret`, {});
  }
}
