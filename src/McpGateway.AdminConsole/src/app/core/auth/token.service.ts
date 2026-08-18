import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { GATEWAY_ADMIN_SCOPE } from '../models/enums';

interface TokenResponse {
  access_token: string;
  token_type: string;
  expires_in: number;
}

interface JwtPayload {
  sub?: string;
  scope?: string;
  identity_type?: string;
}

/**
 * Holds the operator's bearer token in memory only. The token and the raw client
 * secret are never written to localStorage/sessionStorage — a page reload
 * intentionally returns the operator to the login screen. Decoded claims are used
 * for display and to hide operator-only actions; they are never trusted for
 * security, which the gateway enforces server-side.
 */
@Injectable({ providedIn: 'root' })
export class TokenService {
  private readonly _token = signal<string | null>(null);
  private readonly _expiresAt = signal<number | null>(null);
  private readonly _now = signal<number>(browserNow());

  readonly token = this._token.asReadonly();
  readonly isAuthenticated = computed(() => this._token() !== null && this.secondsRemaining() > 0);
  readonly clientId = computed(() => this.claims()?.sub ?? null);
  readonly scopes = computed(() => (this.claims()?.scope ?? '').split(' ').filter((s) => s.length > 0));
  readonly isAdmin = computed(() => this.scopes().includes(GATEWAY_ADMIN_SCOPE));
  readonly secondsRemaining = computed(() => {
    const expiresAt = this._expiresAt();
    if (expiresAt === null) {
      return 0;
    }
    return Math.max(0, Math.round((expiresAt - this._now()) / 1000));
  });

  private readonly claims = computed<JwtPayload | null>(() => {
    const token = this._token();
    return token === null ? null : decodeJwt(token);
  });

  constructor(private readonly http: HttpClient) {
    // Drive the countdown so expiry-derived signals stay live.
    setInterval(() => this._now.set(browserNow()), 1000);
  }

  async login(clientId: string, clientSecret: string): Promise<void> {
    const body = new HttpParamsBody({
      grant_type: 'client_credentials',
      client_id: clientId,
      client_secret: clientSecret,
    });

    const response = await firstValueFrom(
      this.http.post<TokenResponse>(`${environment.apiBaseUrl}/oauth/token`, body.toString(), {
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      }),
    );

    this._token.set(response.access_token);
    this._expiresAt.set(browserNow() + response.expires_in * 1000);
  }

  clear(): void {
    this._token.set(null);
    this._expiresAt.set(null);
  }
}

function browserNow(): number {
  return Date.now();
}

function decodeJwt(token: string): JwtPayload | null {
  const parts = token.split('.');
  if (parts.length !== 3) {
    return null;
  }
  try {
    const json = atob(parts[1].replace(/-/g, '+').replace(/_/g, '/'));
    return JSON.parse(json) as JwtPayload;
  } catch {
    return null;
  }
}

/** Minimal form-encoder so the token request needs no extra dependency. */
class HttpParamsBody {
  constructor(private readonly values: Record<string, string>) {}

  toString(): string {
    return Object.entries(this.values)
      .map(([k, v]) => `${encodeURIComponent(k)}=${encodeURIComponent(v)}`)
      .join('&');
  }
}
