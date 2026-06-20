using DocIntel.Api.Data;
using DocIntel.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DocIntel.Api.Repositories;

public interface IUserRepository
{
    Task<User?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> FindByEmailInWorkspaceAsync(Guid workspaceId, string email, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
}

public interface IWorkspaceRepository
{
    Task<Workspace?> GetAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Workspace workspace, CancellationToken ct = default);
}

public interface IDocumentRepository
{
    Task AddAsync(Document document, CancellationToken ct = default);
    Task<Document?> GetAsync(Guid workspaceId, Guid documentId, CancellationToken ct = default);
    Task<IReadOnlyList<Document>> ListAsync(Guid workspaceId, CancellationToken ct = default);
    Task UpdateAsync(Document document, CancellationToken ct = default);
}

public interface IChunkRepository
{
    Task AddRangeAsync(IEnumerable<DocumentChunk> chunks, CancellationToken ct = default);

    /// <summary>Returns all chunks for a workspace (tenant-scoped retrieval set).</summary>
    Task<IReadOnlyList<DocumentChunk>> ListByWorkspaceAsync(Guid workspaceId, CancellationToken ct = default);
}

// ---- EF Core implementations ----

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;
    public UserRepository(AppDbContext db) => _db = db;

    public Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
        => _db.Users.Include(u => u.Workspace)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> FindByEmailInWorkspaceAsync(Guid workspaceId, string email, CancellationToken ct = default)
        => _db.Users.FirstOrDefaultAsync(u => u.WorkspaceId == workspaceId && u.Email == email, ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
    }
}

public class WorkspaceRepository : IWorkspaceRepository
{
    private readonly AppDbContext _db;
    public WorkspaceRepository(AppDbContext db) => _db = db;

    public Task<Workspace?> GetAsync(Guid id, CancellationToken ct = default)
        => _db.Workspaces.FirstOrDefaultAsync(w => w.Id == id, ct);

    public async Task AddAsync(Workspace workspace, CancellationToken ct = default)
    {
        _db.Workspaces.Add(workspace);
        await _db.SaveChangesAsync(ct);
    }
}

public class DocumentRepository : IDocumentRepository
{
    private readonly AppDbContext _db;
    public DocumentRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(Document document, CancellationToken ct = default)
    {
        _db.Documents.Add(document);
        await _db.SaveChangesAsync(ct);
    }

    public Task<Document?> GetAsync(Guid workspaceId, Guid documentId, CancellationToken ct = default)
        => _db.Documents.FirstOrDefaultAsync(d => d.WorkspaceId == workspaceId && d.Id == documentId, ct);

    public async Task<IReadOnlyList<Document>> ListAsync(Guid workspaceId, CancellationToken ct = default)
        => await _db.Documents
            .Where(d => d.WorkspaceId == workspaceId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task UpdateAsync(Document document, CancellationToken ct = default)
    {
        _db.Documents.Update(document);
        await _db.SaveChangesAsync(ct);
    }
}

public class ChunkRepository : IChunkRepository
{
    private readonly AppDbContext _db;
    public ChunkRepository(AppDbContext db) => _db = db;

    public async Task AddRangeAsync(IEnumerable<DocumentChunk> chunks, CancellationToken ct = default)
    {
        _db.DocumentChunks.AddRange(chunks);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DocumentChunk>> ListByWorkspaceAsync(Guid workspaceId, CancellationToken ct = default)
        => await _db.DocumentChunks
            .Where(c => c.WorkspaceId == workspaceId)
            .ToListAsync(ct);
}
