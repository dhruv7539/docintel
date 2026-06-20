import { Injectable, computed, signal } from '@angular/core';
import { AuthResponse } from './models';

const STORAGE_KEY = 'docintel.auth';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly _session = signal<AuthResponse | null>(this.restore());

  readonly session = this._session.asReadonly();
  readonly isAuthenticated = computed(() => this._session() !== null);

  setSession(session: AuthResponse): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    this._session.set(session);
  }

  clear(): void {
    localStorage.removeItem(STORAGE_KEY);
    this._session.set(null);
  }

  get token(): string | null {
    return this._session()?.token ?? null;
  }

  private restore(): AuthResponse | null {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return null;
    }
    try {
      return JSON.parse(raw) as AuthResponse;
    } catch {
      return null;
    }
  }
}
