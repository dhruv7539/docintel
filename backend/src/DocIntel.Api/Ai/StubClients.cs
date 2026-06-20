using System.Security.Cryptography;
using System.Text;

namespace DocIntel.Api.Ai;

/// <summary>
/// Deterministic, offline embedding client. Produces a stable unit-normalised
/// vector from token hashes so semantically similar text (shared tokens) lands
/// close in vector space. Good enough to exercise the full RAG path in tests
/// and local dev without any API keys. Mirrors DeployIQ's StubLLMClient idea.
/// </summary>
public class StubEmbeddingClient : IEmbeddingClient
{
    private readonly int _dimensions;

    public StubEmbeddingClient(int dimensions = 256)
    {
        _dimensions = dimensions <= 0 ? 256 : dimensions;
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        => Task.FromResult(Embed(text));

    public Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<float[]>>(texts.Select(Embed).ToList());

    private float[] Embed(string text)
    {
        var vector = new float[_dimensions];
        if (string.IsNullOrWhiteSpace(text))
        {
            return vector;
        }

        // Split on whitespace, punctuation, and intra-word separators (hyphen,
        // underscore, slash) so compound terms like "auto-rollback" share tokens
        // with "rollback" and still match during retrieval.
        var tokens = text.ToLowerInvariant()
            .Split(new[] { ' ', '\n', '\t', '.', ',', ';', ':', '!', '?', '(', ')', '"', '\'', '-', '_', '/' },
                StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            var hash = StableHash(token);
            var bucket = (int)(hash % (uint)_dimensions);
            // Sign drawn from a second hash bit keeps the distribution centered.
            var sign = ((hash >> 31) & 1) == 0 ? 1f : -1f;
            vector[bucket] += sign;
        }

        Normalize(vector);
        return vector;
    }

    private static uint StableHash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return BitConverter.ToUInt32(bytes, 0);
    }

    private static void Normalize(float[] vector)
    {
        double norm = 0d;
        foreach (var v in vector)
        {
            norm += v * v;
        }

        norm = Math.Sqrt(norm);
        if (norm == 0d)
        {
            return;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] / norm);
        }
    }
}

/// <summary>
/// Offline LLM client. It does not call any model; instead it composes a clear,
/// grounded answer from the retrieved context so the end-to-end RAG flow returns
/// something meaningful with zero external dependencies.
/// </summary>
public class StubLlmClient : ILlmClient
{
    public Task<string> GenerateAnswerAsync(
        string question,
        IReadOnlyList<RetrievedContext> context,
        CancellationToken ct = default)
    {
        if (context.Count == 0)
        {
            return Task.FromResult(
                "I couldn't find anything relevant in your documents for that question. " +
                "Try uploading more material or rephrasing.");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Based on {context.Count} relevant passage(s) from your documents:");
        sb.AppendLine();

        foreach (var c in context)
        {
            var excerpt = c.Content.Length > 240 ? c.Content[..240] + "..." : c.Content;
            sb.AppendLine($"- ({c.FileName} #{c.Ordinal}) {excerpt}");
        }

        sb.AppendLine();
        sb.AppendLine(
            $"[stub answer] Configure a real Azure OpenAI / OpenAI model to synthesize a " +
            $"natural-language response to: \"{question}\".");

        return Task.FromResult(sb.ToString().TrimEnd());
    }
}
