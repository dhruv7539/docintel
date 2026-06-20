import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-base';
import { AuthResponse, DocumentDto, QueryResponse } from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  register(workspaceName: string, email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.base}/api/auth/register`, {
      workspaceName,
      email,
      password,
    });
  }

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.base}/api/auth/login`, { email, password });
  }

  listDocuments(): Observable<DocumentDto[]> {
    return this.http.get<DocumentDto[]>(`${this.base}/api/documents`);
  }

  uploadText(fileName: string, content: string): Observable<DocumentDto> {
    return this.http.post<DocumentDto>(`${this.base}/api/documents/text`, { fileName, content });
  }

  uploadFile(file: File): Observable<DocumentDto> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<DocumentDto>(`${this.base}/api/documents`, form);
  }

  query(question: string, topK = 4): Observable<QueryResponse> {
    return this.http.post<QueryResponse>(`${this.base}/api/query`, { question, topK });
  }
}
