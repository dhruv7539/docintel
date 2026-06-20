using DocIntel.Api.Ai;
using DocIntel.Api.Models;
using DocIntel.Api.Repositories;
using DocIntel.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DocIntel.Tests;

public class DocumentServiceTests
{
    private readonly Mock<IDocumentRepository> _documents = new();
    private readonly Mock<IChunkRepository> _chunks = new();
    private readonly IEmbeddingClient _embeddings = new StubEmbeddingClient(64);

    private DocumentService CreateSut() => new(
        _documents.Object, _chunks.Object, _embeddings, NullLogger<DocumentService>.Instance);

    [Fact]
    public async Task IngestText_ChunksEmbedsAndMarksIndexed()
    {
        var workspaceId = Guid.NewGuid();
        var capturedChunks = new List<DocumentChunk>();
        _chunks.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<DocumentChunk>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<DocumentChunk>, CancellationToken>((c, _) => capturedChunks.AddRange(c))
            .Returns(Task.CompletedTask);

        var sut = CreateSut();
        var longText = string.Join(" ", Enumerable.Repeat("retrieval augmented generation pipeline", 200));

        var dto = await sut.IngestTextAsync(workspaceId, "notes.txt", longText, "text/plain");

        Assert.Equal("Indexed", dto.Status);
        Assert.True(dto.ChunkCount > 1);
        Assert.All(capturedChunks, c =>
        {
            Assert.Equal(workspaceId, c.WorkspaceId);
            Assert.NotEmpty(c.Embedding);
        });
        _documents.Verify(r => r.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestText_EmptyContentByChunker_StillIndexesWithZeroChunks()
    {
        var sut = CreateSut();
        var dto = await sut.IngestTextAsync(Guid.NewGuid(), "blank.txt", "    ", "text/plain");

        Assert.Equal("Indexed", dto.Status);
        Assert.Equal(0, dto.ChunkCount);
        _chunks.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<DocumentChunk>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Get_MissingDocument_Throws()
    {
        _documents.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        var sut = CreateSut();
        await Assert.ThrowsAsync<NotFoundException>(() => sut.GetAsync(Guid.NewGuid(), Guid.NewGuid()));
    }
}
