export interface AuthResponse {
  token: string;
  email: string;
  workspaceId: string;
  workspaceName: string;
}

export interface DocumentDto {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  status: string;
  chunkCount: number;
  createdAtUtc: string;
}

export interface SourceDto {
  documentId: string;
  fileName: string;
  ordinal: number;
  score: number;
  excerpt: string;
}

export interface QueryResponse {
  answer: string;
  sources: SourceDto[];
}
