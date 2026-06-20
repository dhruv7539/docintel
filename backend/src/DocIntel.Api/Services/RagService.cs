using DocIntel.Api.Ai;
using DocIntel.Api.Dtos;
using DocIntel.Api.Repositories;

namespace DocIntel.Api.Services;

public interface IRagService
{
    Task<QueryResponse> QueryAsync(Guid workspaceId, string question, int topK, CancellationToken ct = default);
}

/// <summary>
/// The retrieval-augmented generation pipeline: embed the question, rank the
/// workspace's chunks by cosine similarity, take the top-K, and ask the LLM to
/// answer grounded in that context. Retrieval is always tenant-scoped.
/// </summary>
public class RagService : IRagService
{
    private readonly IChunkRepository _chunks;
    private readonly IDocumentRepository _documents;
    private readonly IEmbeddingClient _embeddings;
    private readonly ILlmClient _llm;

    public RagService(
        IChunkRepository chunks,
        IDocumentRepository documents,
        IEmbeddingClient embeddings,
        ILlmClient llm)
    {
        _chunks = chunks;
        _documents = documents;
        _embeddings = embeddings;
        _llm = llm;
    }

    public async Task<QueryResponse> QueryAsync(
        Guid workspaceId, string question, int topK, CancellationToken ct = default)
    {
        var chunks = await _chunks.ListByWorkspaceAsync(workspaceId, ct);
        if (chunks.Count == 0)
        {
            var empty = await _llm.GenerateAnswerAsync(question, Array.Empty<RetrievedContext>(), ct);
            return new QueryResponse(empty, Array.Empty<SourceDto>());
        }

        var queryVector = await _embeddings.EmbedAsync(question, ct);

        var ranked = chunks
            .Select(c => (chunk: c, score: VectorMath.CosineSimilarity(queryVector, c.Embedding)))
            .OrderByDescending(x => x.score)
            .Take(Math.Max(1, topK))
            .ToList();

        // Resolve file names once per distinct document for nicer citations.
        var docIds = ranked.Select(r => r.chunk.DocumentId).Distinct();
        var fileNames = new Dictionary<Guid, string>();
        foreach (var id in docIds)
        {
            var doc = await _documents.GetAsync(workspaceId, id, ct);
            fileNames[id] = doc?.FileName ?? "unknown";
        }

        var context = ranked
            .Select(r => new RetrievedContext(
                fileNames[r.chunk.DocumentId], r.chunk.Ordinal, r.chunk.Content, r.score))
            .ToList();

        var answer = await _llm.GenerateAnswerAsync(question, context, ct);

        var sources = ranked.Select(r => new SourceDto(
            r.chunk.DocumentId,
            fileNames[r.chunk.DocumentId],
            r.chunk.Ordinal,
            Math.Round(r.score, 4),
            Excerpt(r.chunk.Content))).ToList();

        return new QueryResponse(answer, sources);
    }

    private static string Excerpt(string content)
        => content.Length > 200 ? content[..200] + "..." : content;
}
