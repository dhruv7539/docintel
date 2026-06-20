import { InjectionToken } from '@angular/core';

/**
 * Base URL for the DocIntel API. Defaults to the local backend, but can be
 * overridden at runtime (e.g. by an entrypoint script writing
 * `window.__DOCINTEL_API__` in the Docker/Azure deployment).
 */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () =>
    (globalThis as any).__DOCINTEL_API__ ?? 'http://localhost:5080',
});
