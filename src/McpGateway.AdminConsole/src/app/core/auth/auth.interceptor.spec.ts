import { TestBed } from '@angular/core/testing';
import {
  HttpClient,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { TokenService } from './token.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;
  let tokenService: jasmine.SpyObj<Pick<TokenService, 'token' | 'clear'>>;
  let router: jasmine.SpyObj<Pick<Router, 'navigate' | 'url'>>;

  beforeEach(() => {
    tokenService = jasmine.createSpyObj('TokenService', ['token', 'clear']);
    router = jasmine.createSpyObj('Router', ['navigate']);
    (router as { url: string }).url = '/tools';

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: TokenService, useValue: tokenService },
        { provide: Router, useValue: router },
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('attaches the bearer token to api requests', () => {
    tokenService.token.and.returnValue('tok-123');

    http.get('/api/tools').subscribe();

    const req = controller.expectOne('/api/tools');
    expect(req.request.headers.get('Authorization')).toBe('Bearer tok-123');
    req.flush([]);
  });

  it('does not attach a token to the oauth endpoint', () => {
    tokenService.token.and.returnValue('tok-123');

    http.post('/oauth/token', 'body').subscribe();

    const req = controller.expectOne('/oauth/token');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });

  it('clears the session and redirects on 401', () => {
    tokenService.token.and.returnValue('tok-123');

    http.get('/api/tools').subscribe({ error: () => {} });

    controller.expectOne('/api/tools').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(tokenService.clear).toHaveBeenCalled();
    expect(router.navigate).toHaveBeenCalledWith(['/login'], jasmine.any(Object));
  });
});
