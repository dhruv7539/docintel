using System.ComponentModel.DataAnnotations;

namespace DocIntel.Api.Models;

/// <summary>
/// A tenant boundary. Every user, document and chunk belongs to exactly one
/// workspace, and all data access is scoped by <see cref="Workspace.Id"/> to
/// guarantee multi-tenant isolation.
/// </summary>
public class Workspace
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkspaceId { get; set; }
    public Workspace? Workspace { get; set; }

    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Role { get; set; } = "member";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public enum DocumentStatus
{
    Pending = 0,
    Indexed = 1,
    Failed = 2
}

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkspaceId { get; set; }
    public Workspace? Workspace { get; set; }

    [MaxLength(400)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ContentType { get; set; } = "text/plain";

    public long SizeBytes { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

    public int ChunkCount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}

/// <summary>
/// A contiguous slice of a document together with its embedding vector. Chunks
/// are the unit of retrieval for the RAG pipeline.
/// </summary>
public class DocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid DocumentId { get; set; }
    public Document? Document { get; set; }

    // Denormalised tenant id so retrieval can filter without a join.
    public Guid WorkspaceId { get; set; }

    public int Ordinal { get; set; }

    public string Content { get; set; } = string.Empty;

    /// <summary>Embedding vector, stored as a primitive float array.</summary>
    public float[] Embedding { get; set; } = Array.Empty<float>();
}
