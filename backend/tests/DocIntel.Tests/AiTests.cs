using DocIntel.Api.Ai;
using Xunit;

namespace DocIntel.Tests;

public class VectorMathTests
{
    [Fact]
    public void CosineSimilarity_IdenticalVectors_IsOne()
    {
        var v = new[] { 1f, 2f, 3f };
        Assert.Equal(1.0, VectorMath.CosineSimilarity(v, v), 5);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_IsZero()
    {
        Assert.Equal(0.0, VectorMath.CosineSimilarity(new[] { 1f, 0f }, new[] { 0f, 1f }), 5);
    }

    [Fact]
    public void CosineSimilarity_MismatchedLengths_ReturnsZero()
    {
        Assert.Equal(0.0, VectorMath.CosineSimilarity(new[] { 1f, 2f }, new[] { 1f }));
    }
}

public class TextChunkerTests
{
    [Fact]
    public void Chunk_ShortText_ReturnsSingleChunk()
    {
        var chunks = TextChunker.Chunk("a short sentence");
        Assert.Single(chunks);
    }

    [Fact]
    public void Chunk_LongText_SplitsWithOverlap()
    {
        var text = string.Join(" ", Enumerable.Repeat("word", 1000));
        var chunks = TextChunker.Chunk(text, maxChars: 200, overlapChars: 40);
        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Length <= 200));
    }

    [Fact]
    public void Chunk_EmptyText_ReturnsEmpty()
    {
        Assert.Empty(TextChunker.Chunk("   "));
    }
}

public class StubEmbeddingClientTests
{
    [Fact]
    public async Task Embed_IsDeterministic()
    {
        var client = new StubEmbeddingClient(128);
        var a = await client.EmbedAsync("kubernetes deployment rollback");
        var b = await client.EmbedAsync("kubernetes deployment rollback");
        Assert.Equal(a, b);
    }

    [Fact]
    public async Task Embed_SimilarText_RanksHigherThanUnrelated()
    {
        var client = new StubEmbeddingClient(256);
        var query = await client.EmbedAsync("how do I configure database connection pooling");
        var related = await client.EmbedAsync("database connection pooling configuration guide");
        var unrelated = await client.EmbedAsync("the weather today is sunny and warm");

        var relatedScore = VectorMath.CosineSimilarity(query, related);
        var unrelatedScore = VectorMath.CosineSimilarity(query, unrelated);

        Assert.True(relatedScore > unrelatedScore);
    }

    [Fact]
    public async Task Embed_CompoundTerm_MatchesItsParts()
    {
        var client = new StubEmbeddingClient(256);
        var query = await client.EmbedAsync("how fast is rollback");
        var doc = await client.EmbedAsync("Helm SDK auto-rollback brings MTTR under 30 seconds");

        // "auto-rollback" is split into "auto" + "rollback", so it overlaps the
        // query token "rollback" and yields a non-zero similarity.
        Assert.True(VectorMath.CosineSimilarity(query, doc) > 0);
    }
}

public class StubLlmClientTests
{
    [Fact]
    public async Task GenerateAnswer_NoContext_ReturnsFallback()
    {
        var llm = new StubLlmClient();
        var answer = await llm.GenerateAnswerAsync("anything", Array.Empty<RetrievedContext>());
        Assert.Contains("couldn't find", answer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAnswer_WithContext_CitesSources()
    {
        var llm = new StubLlmClient();
        var ctx = new[] { new RetrievedContext("guide.txt", 0, "Connection pooling improves throughput.", 0.9) };
        var answer = await llm.GenerateAnswerAsync("how to scale?", ctx);
        Assert.Contains("guide.txt", answer);
    }
}
