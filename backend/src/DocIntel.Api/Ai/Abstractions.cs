namespace DocIntel.Api.Ai;

/// <summary>A single retrieved chunk passed to the LLM as grounding context.</summary>
public record RetrievedContext(string FileName, int Ordinal, string Content, double Score);

/// <summary>
/// LLM completion abstraction. Implemented by <see cref="StubLlmClient"/> for
/// offline runs/tests and <see cref="AzureOpenAiLlmClient"/> for real models.
/// </summary>
public interface ILlmClient
{
    Task<string> GenerateAnswerAsync(
        string question,
        IReadOnlyList<RetrievedContext> context,
        CancellationToken ct = default);
}

/// <summary>Turns text into an embedding vector for semantic search.</summary>
public interface IEmbeddingClient
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);

    Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default);
}
