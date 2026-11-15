using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Oregon.Backend.Data;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build the context without starting the app (and therefore without
/// needing Temporal, secrets or a reachable database).
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OregonDbContext>
{
    public OregonDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<OregonDbContext> builder = new();
        builder.UseNpgsql("Host=localhost;Database=oregon_design").UseSnakeCaseNamingConvention();
        return new OregonDbContext(builder.Options);
    }
}
