using DocIntel.Api.Ai;
using DocIntel.Api.Models;
using DocIntel.Api.Repositories;
using DocIntel.Api.Services;
using Moq;
using Xunit;

namespace DocIntel.Tests;

public class RagServiceTests
{
    private readonly Mock<IChunkRepository> _chunks = new();
    private readonly Mock<IDocumentRepository> _documents = new();
    private readonly IEmbeddingClient _embeddings = new StubEmbeddingClient(256);
    private readonly ILlmClient _llm = new StubLlmClient();

    private RagService CreateSut() =>
        new(_chunks.Object, _documents.Object, _embeddings, _llm);

    [Fact]
    public async Task Query_NoChunks_ReturnsFallbackAndNoSources()
    {
        _chunks.Setup(r => r.ListByWorkspaceAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DocumentChunk>());

        var sut = CreateSut();
        var result = await sut.QueryAsync(Guid.NewGuid(), "anything", 4);

        Assert.Empty(result.Sources);
        Assert.False(string.IsNullOrWhiteSpace(result.Answer));
    }

    [Fact]
    public async Task Query_RanksMostRelevantChunkFirst()
    {
        var workspaceId = Guid.NewGuid();
        var docId = Guid.NewGuid();

        async Task<DocumentChunk> MakeChunk(int ord, string text)
            => new()
            {
                DocumentId = docId,
                WorkspaceId = workspaceId,
                Ordinal = ord,
                Content = text,
                Embedding = await _embeddings.EmbedAsync(text)
            };

        var stored = new List<DocumentChunk>
        {
            await MakeChunk(0, "the cafeteria menu features pasta and salad on fridays"),
            await MakeChunk(1, "kubernetes horizontal pod autoscaler scales replicas based on cpu"),
            await MakeChunk(2, "annual leave policy grants twenty paid vacation days")
        };

        _chunks.Setup(r => r.ListByWorkspaceAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);
        _documents.Setup(r => r.GetAsync(workspaceId, docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = docId, WorkspaceId = workspaceId, FileName = "handbook.txt" });

        var sut = CreateSut();
        var result = await sut.QueryAsync(workspaceId, "how does kubernetes autoscaling work with cpu", 2);

        Assert.NotEmpty(result.Sources);
        Assert.Equal(1, result.Sources[0].Ordinal); // the kubernetes chunk
        Assert.Equal("handbook.txt", result.Sources[0].FileName);
        Assert.True(result.Sources[0].Score >= result.Sources[^1].Score);
    }

    [Fact]
    public async Task Query_RespectsTopK()
    {
        var workspaceId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var stored = new List<DocumentChunk>();
        for (var i = 0; i < 5; i++)
        {
            stored.Add(new DocumentChunk
            {
                DocumentId = docId,
                WorkspaceId = workspaceId,
                Ordinal = i,
                Content = $"chunk number {i} about distributed systems and reliability",
                Embedding = await _embeddings.EmbedAsync($"chunk {i} distributed systems")
            });
        }

        _chunks.Setup(r => r.ListByWorkspaceAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);
        _documents.Setup(r => r.GetAsync(workspaceId, docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Document { Id = docId, WorkspaceId = workspaceId, FileName = "doc.txt" });

        var sut = CreateSut();
        var result = await sut.QueryAsync(workspaceId, "distributed systems", topK: 3);

        Assert.Equal(3, result.Sources.Count);
    }
}
