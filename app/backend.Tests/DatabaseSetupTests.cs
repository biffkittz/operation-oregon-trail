using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Oregon.Backend.Configuration;
using Oregon.Backend.Data;
using Oregon.Backend.Tests.Support;

namespace Oregon.Backend.Tests;

/// <summary>The migrate command: least-privilege app role and the Data Protection key purge, on a real PostgreSQL.</summary>
[Collection(nameof(ApiCollection))]
public sealed class DatabaseSetupTests(BackendFactory factory)
{
    private const string Role = "oregon_app_test";
    private const string RolePassword = "Gen3rated-pass_word.42~";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Setup_CreatesADataOnlyRole_AndPurgesUnencryptedKeys()
    {
        string adminConnection = await CreateIsolatedDatabaseAsync();
        DatabaseSettings settings = new() { AppRoleName = Role, AppRolePassword = RolePassword, PurgeUnprotectedDataProtectionKeys = true };

        await using (OregonDbContext admin = Context(adminConnection))
        {
            await admin.Database.MigrateAsync(Ct);
            await admin.Database.ExecuteSqlRawAsync(
                "INSERT INTO data_protection_keys (friendly_name, xml) VALUES ('plain', '<key><descriptor /></key>'), ('wrapped', '<key><encryptedSecret /></key>')",
                Ct);
            await DatabaseSetup.RunAsync(admin, settings, NullLogger.Instance, Ct);
            await DatabaseSetup.RunAsync(admin, settings, NullLogger.Instance, Ct); // idempotent

            List<string?> remaining = await admin.Database.SqlQueryRaw<string?>("SELECT friendly_name AS \"Value\" FROM data_protection_keys").ToListAsync(Ct);
            Assert.Equal(["wrapped"], remaining);
        }

        // Signing in with the plaintext password proves the SCRAM verifier was computed correctly.
        string appConnection = new NpgsqlConnectionStringBuilder(adminConnection) { Username = Role, Password = RolePassword }.ConnectionString;
        await using (OregonDbContext app = Context(appConnection))
        {
            app.Settings.Add(new AppSettingsRecord());
            await app.SaveChangesAsync(Ct);
            Assert.Equal(1, await app.Settings.CountAsync(Ct));
            await app.Settings.ExecuteDeleteAsync(Ct);
        }

        await using NpgsqlConnection connection = new(appConnection);
        await connection.OpenAsync(Ct);
        foreach (string ddl in new[] { "CREATE TABLE intruder (id int)", "DROP TABLE games", "TRUNCATE games", "ALTER TABLE games ADD COLUMN x int" })
        {
            await using NpgsqlCommand command = new(ddl, connection);
            PostgresException denied = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Ct));
            Assert.True(denied.SqlState is PostgresErrorCodes.InsufficientPrivilege, $"{ddl}: {denied.SqlState}");
        }
    }

    [Fact]
    public void ScramVerifier_HasPostgresFormat_AndIsSaltedPerCall()
    {
        string first = DatabaseSetup.ScramVerifier("secret");
        string second = DatabaseSetup.ScramVerifier("secret");

        Assert.Matches(@"^SCRAM-SHA-256\$4096:[A-Za-z0-9+/=]+\$[A-Za-z0-9+/=]+:[A-Za-z0-9+/=]+$", first);
        Assert.NotEqual(first, second);
    }

    private static OregonDbContext Context(string connectionString) =>
        new(new DbContextOptionsBuilder<OregonDbContext>().UseNpgsql(connectionString).UseSnakeCaseNamingConvention().Options);

    /// <summary>
    /// A fresh database owned by an admin that, like Azure Database for PostgreSQL's administrator login, is NOT a
    /// superuser and only has CREATEROLE. Running the setup as the container's real superuser would hide statements
    /// Azure rejects (e.g. any mention of the SUPERUSER attribute).
    /// </summary>
    private async Task<string> CreateIsolatedDatabaseAsync()
    {
        string suffix = Guid.NewGuid().ToString("N")[..12];
        string database = $"setup_{suffix}";
        string admin = $"azure_like_admin_{suffix}";
        const string adminPassword = "Admin-pass_word.42~";
        await using NpgsqlConnection connection = new(factory.AdminConnectionString);
        await connection.OpenAsync(Ct);
        foreach (string sql in new[]
        {
            $"CREATE ROLE {admin} LOGIN CREATEROLE NOSUPERUSER PASSWORD '{adminPassword}'",
            $"CREATE DATABASE \"{database}\" OWNER {admin}",
        })
        {
            await using NpgsqlCommand command = new(sql, connection);
            await command.ExecuteNonQueryAsync(Ct);
        }

        return new NpgsqlConnectionStringBuilder(factory.AdminConnectionString) { Database = database, Username = admin, Password = adminPassword }.ConnectionString;
    }
}
