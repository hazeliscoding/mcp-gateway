import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { TokenService } from './token.service';
import { environment } from '../../../environments/environment';

// A token whose payload carries sub + the admin scope. Signature is irrelevant to
// the client, which decodes claims for display only.
function makeJwt(payload: Record<string, unknown>): string {
  const b64 = (obj: unknown) => btoa(JSON.stringify(obj)).replace(/=+$/, '');
  return `${b64({ alg: 'HS256' })}.${b64(payload)}.sig`;
}

describe('TokenService', () => {
  let service: TokenService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(TokenService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('starts unauthenticated', () => {
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.secondsRemaining()).toBe(0);
  });

  it('stores the token, decodes claims, and computes remaining seconds', async () => {
    const token = makeJwt({ sub: 'gateway_admin', scope: 'gateway.admin queue.read' });
    const login = service.login('gateway_admin', 'secret');

    const req = http.expectOne(`${environment.apiBaseUrl}/oauth/token`);
    expect(req.request.body).toContain('grant_type=client_credentials');
    req.flush({ access_token: token, token_type: 'Bearer', expires_in: 900 });
    await login;

    expect(service.isAuthenticated()).toBeTrue();
    expect(service.clientId()).toBe('gateway_admin');
    expect(service.isAdmin()).toBeTrue();
    expect(service.secondsRemaining()).toBeGreaterThan(890);
    expect(service.secondsRemaining()).toBeLessThanOrEqual(900);
  });

  it('treats a non-admin scope as read-only', async () => {
    const token = makeJwt({ sub: 'agent', scope: 'queue.read' });
    const login = service.login('agent', 'secret');
    http.expectOne(`${environment.apiBaseUrl}/oauth/token`).flush({
      access_token: token,
      token_type: 'Bearer',
      expires_in: 900,
    });
    await login;

    expect(service.isAdmin()).toBeFalse();
  });

  it('clears the session', async () => {
    const token = makeJwt({ sub: 'gateway_admin', scope: 'gateway.admin' });
    const login = service.login('gateway_admin', 'secret');
    http.expectOne(`${environment.apiBaseUrl}/oauth/token`).flush({
      access_token: token,
      token_type: 'Bearer',
      expires_in: 900,
    });
    await login;

    service.clear();

    expect(service.isAuthenticated()).toBeFalse();
    expect(service.clientId()).toBeNull();
  });
});
