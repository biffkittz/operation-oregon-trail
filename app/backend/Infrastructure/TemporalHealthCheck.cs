using Microsoft.Extensions.Diagnostics.HealthChecks;
using Oregon.Backend.Games;

namespace Oregon.Backend.Infrastructure;

/// <summary>Readiness check: can the backend reach Temporal?</summary>
internal sealed class TemporalHealthCheck(IGameEngine engine) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        await engine.IsHealthyAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Temporal is unreachable.");
}
