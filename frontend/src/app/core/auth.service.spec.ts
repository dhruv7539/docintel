import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';
import { AuthResponse } from './models';

const session: AuthResponse = {
  token: 'jwt-token',
  email: 'you@acme.com',
  workspaceId: 'ws-1',
  workspaceName: 'Acme',
};

describe('AuthService', () => {
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
  });

  it('starts unauthenticated with no stored session', () => {
    const auth = TestBed.inject(AuthService);
    expect(auth.isAuthenticated()).toBeFalse();
    expect(auth.token).toBeNull();
  });

  it('stores the session and exposes the token', () => {
    const auth = TestBed.inject(AuthService);
    auth.setSession(session);
    expect(auth.isAuthenticated()).toBeTrue();
    expect(auth.token).toBe('jwt-token');
    expect(localStorage.getItem('docintel.auth')).toContain('jwt-token');
  });

  it('clears the session', () => {
    const auth = TestBed.inject(AuthService);
    auth.setSession(session);
    auth.clear();
    expect(auth.isAuthenticated()).toBeFalse();
    expect(localStorage.getItem('docintel.auth')).toBeNull();
  });

  it('restores a persisted session on construction', () => {
    localStorage.setItem('docintel.auth', JSON.stringify(session));
    const auth = TestBed.inject(AuthService);
    expect(auth.isAuthenticated()).toBeTrue();
    expect(auth.session()?.workspaceName).toBe('Acme');
  });

  it('ignores corrupt persisted session data', () => {
    localStorage.setItem('docintel.auth', 'not-json');
    const auth = TestBed.inject(AuthService);
    expect(auth.isAuthenticated()).toBeFalse();
  });
});
