import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ApiService } from './api.service';
import { API_BASE_URL } from './api-base';

describe('ApiService', () => {
  let api: ApiService;
  let httpMock: HttpTestingController;
  const base = 'http://test-host';

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: base },
      ],
    });
    api = TestBed.inject(ApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('posts registration details', () => {
    api.register('Acme', 'you@acme.com', 'pw').subscribe();
    const req = httpMock.expectOne(`${base}/api/auth/register`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      workspaceName: 'Acme',
      email: 'you@acme.com',
      password: 'pw',
    });
    req.flush({});
  });

  it('posts login credentials', () => {
    api.login('you@acme.com', 'pw').subscribe();
    const req = httpMock.expectOne(`${base}/api/auth/login`);
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('uploads a file as multipart form data', () => {
    const file = new File(['hello'], 'notes.txt', { type: 'text/plain' });
    api.uploadFile(file).subscribe();
    const req = httpMock.expectOne(`${base}/api/documents`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body instanceof FormData).toBeTrue();
    req.flush({});
  });

  it('sends a query with the default topK', () => {
    api.query('how fast is rollback?').subscribe();
    const req = httpMock.expectOne(`${base}/api/query`);
    expect(req.request.body).toEqual({ question: 'how fast is rollback?', topK: 4 });
    req.flush({ answer: '', sources: [] });
  });
});
