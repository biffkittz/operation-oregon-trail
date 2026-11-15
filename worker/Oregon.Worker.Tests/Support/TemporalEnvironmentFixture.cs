using Oregon.Worker.Tests.Support;
using Temporalio.Testing;

[assembly: AssemblyFixture(typeof(TemporalEnvironmentFixture))]

namespace Oregon.Worker.Tests.Support;

/// <summary>
/// One local Temporal dev server for the whole test assembly. Uses the installed Temporal CLI when present
/// (TEMPORAL_CLI_PATH or PATH); otherwise the SDK downloads a dev server binary on first use.
/// </summary>
public sealed class TemporalEnvironmentFixture : IAsyncLifetime
{
    public WorkflowEnvironment Environment { get; private set; } = null!;

    public async ValueTask InitializeAsync() =>
        Environment = await WorkflowEnvironment.StartLocalAsync(new WorkflowEnvironmentStartLocalOptions
        {
            DevServerOptions = new DevServerOptions { ExistingPath = FindTemporalCli(), LogLevel = "error" },
        });

    public async ValueTask DisposeAsync() => await Environment.ShutdownAsync();

    private static string? FindTemporalCli()
    {
        string? configured = System.Environment.GetEnvironmentVariable("TEMPORAL_CLI_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return configured;
        }

        string path = System.Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(dir => Path.Combine(dir, "temporal"))
            .FirstOrDefault(File.Exists);
    }
}
