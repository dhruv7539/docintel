namespace DocIntel.Api.Dtos;

// ---- Auth ----

public record RegisterRequest(string WorkspaceName, string Email, string Password);

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, string Email, Guid WorkspaceId, string WorkspaceName);

// ---- Documents ----

public record DocumentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Status,
    int ChunkCount,
    DateTime CreatedAtUtc);

public record UploadTextRequest(string FileName, string Content);

// ---- RAG query ----

public record QueryRequest(string Question, int TopK = 4);

public record SourceDto(Guid DocumentId, string FileName, int Ordinal, double Score, string Excerpt);

public record QueryResponse(string Answer, IReadOnlyList<SourceDto> Sources);
