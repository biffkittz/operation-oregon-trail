using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Oregon.Backend.Configuration;
using Oregon.Backend.Infrastructure;

namespace Oregon.Backend.Data;

/// <summary>
/// The <c>migrate</c> command (<c>dotnet Oregon.Backend.dll migrate</c>), run by the Container Apps job
/// <c>job-oregon-migrate</c> as the database admin before each web release:
/// <list type="number">
/// <item>applies EF Core migrations;</item>
/// <item>creates or updates the least-privilege role the web app connects as (data access, no DDL);</item>
/// <item>optionally deletes Data Protection keys stored without Key Vault encryption.</item>
/// </list>
/// Splitting it out means the always-on web app never holds admin credentials.
/// </summary>
public static partial class DatabaseSetup
{
    /// <summary>First command-line argument that selects this command.</summary>
    public const string Command = "migrate";

    /// <summary>PostgreSQL's default SCRAM iteration count.</summary>
    private const int ScramIterations = 4096;

    /// <summary>Runs the command with the app's normal configuration sources (appsettings, environment, args).</summary>
    /// <param name="args">Remaining command-line arguments (configuration overrides).</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddOregonDbContext();
        builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection(DatabaseSettings.SectionName));
        using IHost host = builder.Build();
        ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseSetup));
        DatabaseSettings settings = host.Services.GetRequiredService<IOptions<DatabaseSettings>>().Value;

        try
        {
            await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
            OregonDbContext db = scope.ServiceProvider.GetRequiredService<OregonDbContext>();
            await RunAsync(db, settings, logger, CancellationToken.None);
            return 0;
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or ArgumentException)
        {
            LogFailed(logger, ex);
            return 1;
        }
    }

    /// <summary>Applies migrations, the app role and the key purge to an open context.</summary>
    /// <param name="db">Context connected as the database admin.</param>
    /// <param name="settings">What to do.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public static async Task RunAsync(OregonDbContext db, DatabaseSettings settings, ILogger logger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);

        await db.Database.MigrateAsync(cancellationToken);
        LogMigrated(logger, db.Database.GetMigrations().LastOrDefault() ?? "none");

        if (!string.IsNullOrWhiteSpace(settings.AppRoleName))
        {
            if (string.IsNullOrEmpty(settings.AppRolePassword))
            {
                throw new InvalidOperationException("Database:AppRolePassword is required when Database:AppRoleName is set.");
            }

            await EnsureAppRoleAsync(db, settings.AppRoleName, settings.AppRolePassword, cancellationToken);
            LogRoleReady(logger, settings.AppRoleName);
        }

        if (settings.PurgeUnprotectedDataProtectionKeys)
        {
            // Key Vault-wrapped keys carry an <encryptedSecret> element; anything else was stored in the clear.
            int purged = await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM data_protection_keys WHERE xml IS NULL OR xml NOT LIKE '%<encryptedSecret%'",
                cancellationToken);
            LogKeysPurged(logger, purged);
        }
    }

    /// <summary>
    /// Creates the role if needed, (re)sets its password and grants read/write on the app's tables and sequences in
    /// schema public, now and for tables future migrations create. No CREATE, ALTER, DROP or TRUNCATE.
    /// </summary>
    private static async Task EnsureAppRoleAsync(OregonDbContext db, string role, string password, CancellationToken cancellationToken)
    {
        if (!RoleName().IsMatch(role))
        {
            throw new ArgumentException("Database:AppRoleName must be a lower-case PostgreSQL identifier.", nameof(role));
        }

        // The password is sent as a SCRAM-SHA-256 verifier, never as plaintext, so it can't surface in server logs.
        string verifier = ScramVerifier(password);
        string[] statements =
        [
            "SELECT format('CREATE ROLE %I LOGIN', @role) WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = @role)",
            // No NOSUPERUSER/NOCREATEDB/NOCREATEROLE: new roles have none of those anyway, and on PostgreSQL 16+ only a
            // superuser may mention the SUPERUSER attribute at all; Azure's admin login is not one (42501).
            "SELECT format('ALTER ROLE %I WITH LOGIN PASSWORD %L', @role, @verifier)",
            "SELECT format('GRANT CONNECT ON DATABASE %I TO %I', current_database(), @role)",
            "SELECT format('GRANT USAGE ON SCHEMA public TO %I', @role)",
            "SELECT format('REVOKE CREATE ON SCHEMA public FROM %I', @role)",
            "SELECT format('GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO %I', @role)",
            "SELECT format('GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA public TO %I', @role)",
            "SELECT format('ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO %I', @role)",
            "SELECT format('ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT, UPDATE ON SEQUENCES TO %I', @role)",
        ];

        DbConnection connection = db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            foreach (string template in statements)
            {
                // DDL can't take bind parameters, so let the server quote them: format() with %I (identifier) and %L
                // (literal) builds the statement, which is then executed as-is.
                string? ddl;
                await using (DbCommand build = connection.CreateCommand())
                {
                    build.CommandText = template;
                    AddParameter(build, "role", role);
                    AddParameter(build, "verifier", verifier);
                    ddl = await build.ExecuteScalarAsync(cancellationToken) as string;
                }

                if (ddl is null)
                {
                    continue; // CREATE ROLE when the role already exists.
                }

                await using DbCommand run = connection.CreateCommand();
                run.CommandText = ddl;
                await run.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    /// <summary>
    /// Builds a PostgreSQL SCRAM-SHA-256 password verifier (RFC 5802/7677), the same format the server stores, so
    /// the plaintext never leaves this process. Passwords here are ASCII (generated), so SASLprep is the identity.
    /// </summary>
    /// <param name="password">The plaintext password.</param>
    /// <returns>The verifier string <c>SCRAM-SHA-256$iterations:salt$StoredKey:ServerKey</c>.</returns>
    internal static string ScramVerifier(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] salted = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, ScramIterations, HashAlgorithmName.SHA256, 32);
        byte[] clientKey = HMACSHA256.HashData(salted, "Client Key"u8);
        byte[] storedKey = SHA256.HashData(clientKey);
        byte[] serverKey = HMACSHA256.HashData(salted, "Server Key"u8);
        return $"SCRAM-SHA-256${ScramIterations}:{Convert.ToBase64String(salt)}${Convert.ToBase64String(storedKey)}:{Convert.ToBase64String(serverKey)}";
    }

    private static void AddParameter(DbCommand command, string name, string value)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    [GeneratedRegex("^[a-z_][a-z0-9_]{0,62}$")]
    private static partial Regex RoleName();

    [LoggerMessage(Level = LogLevel.Information, Message = "Database schema is up to date (latest migration: {Migration})")]
    private static partial void LogMigrated(ILogger logger, string migration);

    [LoggerMessage(Level = LogLevel.Information, Message = "Application role {Role} is ready (data access only, no DDL)")]
    private static partial void LogRoleReady(ILogger logger, string role);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted {Count} Data Protection keys that were not encrypted at rest")]
    private static partial void LogKeysPurged(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Database setup failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
