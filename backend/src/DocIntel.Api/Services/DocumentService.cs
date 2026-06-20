using DocIntel.Api.Ai;
using DocIntel.Api.Dtos;
using DocIntel.Api.Models;
using DocIntel.Api.Repositories;
using Microsoft.Extensions.Logging;

namespace DocIntel.Api.Services;

public interface IDocumentService
{
    Task<DocumentDto> IngestTextAsync(Guid workspaceId, string fileName, string content, string contentType, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentDto>> ListAsync(Guid workspaceId, CancellationToken ct = default);
    Task<DocumentDto> GetAsync(Guid workspaceId, Guid documentId, CancellationToken ct = default);
}

/// <summary>
/// Owns the ingestion/indexing pipeline: persist the document, split it into
/// overlapping chunks, embed each chunk, and store the vectors so they can be
/// retrieved later by <see cref="RagService"/>. All work is tenant-scoped.
/// </summary>
public class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documents;
    private readonly IChunkRepository _chunks;
    private readonly IEmbeddingClient _embeddings;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        IDocumentRepository documents,
        IChunkRepository chunks,
        IEmbeddingClient embeddings,
        ILogger<DocumentService> logger)
    {
        _documents = documents;
        _chunks = chunks;
        _embeddings = embeddings;
        _logger = logger;
    }

    public async Task<DocumentDto> IngestTextAsync(
        Guid workspaceId, string fileName, string content, string contentType, CancellationToken ct = default)
    {
        var document = new Document
        {
            WorkspaceId = workspaceId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = System.Text.Encoding.UTF8.GetByteCount(content),
            Status = DocumentStatus.Pending
        };
        await _documents.AddAsync(document, ct);

        try
        {
            var pieces = TextChunker.Chunk(content);
            if (pieces.Count == 0)
            {
                document.Status = DocumentStatus.Indexed;
                document.ChunkCount = 0;
                await _documents.UpdateAsync(document, ct);
                return ToDto(document);
            }

            var vectors = await _embeddings.EmbedBatchAsync(pieces, ct);

            var chunks = new List<DocumentChunk>(pieces.Count);
            for (var i = 0; i < pieces.Count; i++)
            {
                chunks.Add(new DocumentChunk
                {
                    DocumentId = document.Id,
                    WorkspaceId = workspaceId,
                    Ordinal = i,
                    Content = pieces[i],
                    Embedding = i < vectors.Count ? vectors[i] : Array.Empty<float>()
                });
            }

            await _chunks.AddRangeAsync(chunks, ct);

            document.ChunkCount = chunks.Count;
            document.Status = DocumentStatus.Indexed;
            await _documents.UpdateAsync(document, ct);

            _logger.LogInformation(
                "Indexed document {DocumentId} ({Chunks} chunks) in workspace {WorkspaceId}",
                document.Id, chunks.Count, workspaceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index document {DocumentId}", document.Id);
            document.Status = DocumentStatus.Failed;
            await _documents.UpdateAsync(document, ct);
            throw;
        }

        return ToDto(document);
    }

    public async Task<IReadOnlyList<DocumentDto>> ListAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var docs = await _documents.ListAsync(workspaceId, ct);
        return docs.Select(ToDto).ToList();
    }

    public async Task<DocumentDto> GetAsync(Guid workspaceId, Guid documentId, CancellationToken ct = default)
    {
        var doc = await _documents.GetAsync(workspaceId, documentId, ct)
                  ?? throw new NotFoundException("Document not found.");
        return ToDto(doc);
    }

    private static DocumentDto ToDto(Document d) => new(
        d.Id, d.FileName, d.ContentType, d.SizeBytes, d.Status.ToString(), d.ChunkCount, d.CreatedAtUtc);
}
