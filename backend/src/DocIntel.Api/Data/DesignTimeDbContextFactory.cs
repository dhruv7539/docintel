using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DocIntel.Api.Data;

/// <summary>
/// Used only by the EF Core CLI (`dotnet ef migrations ...`). It pins the
/// PostgreSQL provider so migrations are generated for the production database
/// regardless of the runtime "Database:Provider" setting.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                   ?? "Host=localhost;Port=5432;Database=docintel;Username=docintel;Password=docintel";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn)
            .Options;

        return new AppDbContext(options);
    }
}
