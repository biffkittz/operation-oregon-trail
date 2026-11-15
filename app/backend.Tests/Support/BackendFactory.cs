using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Oregon.Backend.Games;
using Oregon.Backend.Tests.Support;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(BackendFactory))]

namespace Oregon.Backend.Tests.Support;

/// <summary>
/// One Postgres container and one in-process backend for the whole assembly. Temporal is replaced by
/// <see cref="FakeGameEngine"/> and the embedded projection worker is switched off.
/// </summary>
public sealed class BackendFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "test-password-123";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public FakeGameEngine Engine { get; } = new();

    /// <summary>Gets the superuser connection string of the test PostgreSQL container.</summary>
    public string AdminConnectionString => _postgres.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // Read eagerly by Program at startup, so they are supplied as environment variables (before the host builds).
        Environment.SetEnvironmentVariable("ConnectionStrings__Oregon", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("Auth__Password", Password);
        Environment.SetEnvironmentVariable("Temporal__RunWebWorker", "false");
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IGameEngine>();
            services.AddSingleton<IGameEngine>(Engine);
        });
    }
}
