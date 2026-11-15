using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Oregon.Backend.Configuration;

namespace Oregon.Backend.Data;

/// <summary>
/// Applies EF Core migrations before anything else starts (it is registered as the first hosted service, so the
/// projection worker and Kestrel only start once the schema exists).
/// </summary>
/// <remarks>
/// On a brand-new database EF Core logs one "Failed executing DbCommand ... __EFMigrationsHistory" error while
/// it probes for the history table. That is expected on first start only; the migration then proceeds.
/// </remarks>
internal sealed partial class DatabaseMigrator(
    IServiceScopeFactory scopes,
    IOptions<DatabaseSettings> settings,
    ILogger<DatabaseMigrator> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!settings.Value.MigrateOnStartup)
        {
            return;
        }

        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        OregonDbContext db = scope.ServiceProvider.GetRequiredService<OregonDbContext>();

        // MigrateAsync is idempotent and takes a database lock, so it is safe even if two replicas start at once.
        await db.Database.MigrateAsync(cancellationToken);
        LogSchemaReady(logger, db.Database.GetMigrations().LastOrDefault() ?? "none");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Database schema is up to date (latest migration: {Migration})")]
    private static partial void LogSchemaReady(ILogger logger, string migration);
}
