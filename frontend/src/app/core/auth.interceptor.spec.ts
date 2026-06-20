import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => httpMock.verify());

  it('adds a bearer token when authenticated', () => {
    auth.setSession({ token: 'jwt-token', email: 'e', workspaceId: 'w', workspaceName: 'n' });
    http.get('/api/documents').subscribe();
    const req = httpMock.expectOne('/api/documents');
    expect(req.request.headers.get('Authorization')).toBe('Bearer jwt-token');
    req.flush([]);
  });

  it('leaves requests unauthenticated when there is no token', () => {
    http.get('/api/documents').subscribe();
    const req = httpMock.expectOne('/api/documents');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush([]);
  });
});
