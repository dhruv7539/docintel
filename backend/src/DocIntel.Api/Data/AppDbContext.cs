using System.Text.Json;
using DocIntel.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DocIntel.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => new { u.WorkspaceId, u.Email }).IsUnique();
            e.HasOne(u => u.Workspace)
                .WithMany(w => w.Users)
                .HasForeignKey(u => u.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Document>(e =>
        {
            e.HasIndex(d => d.WorkspaceId);
            e.HasOne(d => d.Workspace)
                .WithMany(w => w.Documents)
                .HasForeignKey(d => d.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Embeddings are stored as JSON for portability across the in-memory,
        // PostgreSQL and Azure SQL providers. A production deployment would swap
        // this for a native vector column (pgvector / Azure AI Search).
        var floatArrayConverter = new ValueConverter<float[], string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<float[]>(v, (JsonSerializerOptions?)null) ?? Array.Empty<float>());

        var floatArrayComparer = new ValueComparer<float[]>(
            (a, b) => (a ?? Array.Empty<float>()).SequenceEqual(b ?? Array.Empty<float>()),
            v => v.Aggregate(0, (acc, f) => HashCode.Combine(acc, f.GetHashCode())),
            v => v.ToArray());

        modelBuilder.Entity<DocumentChunk>(e =>
        {
            e.HasIndex(c => c.WorkspaceId);
            e.HasIndex(c => c.DocumentId);
            e.Property(c => c.Embedding)
                .HasConversion(floatArrayConverter, floatArrayComparer)
                .HasColumnType("text");
            e.HasOne(c => c.Document)
                .WithMany(d => d.Chunks)
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
