using Temporalio.Activities;

namespace Oregon.Contracts.Workflows;

/// <summary>
/// Activities implemented by the backend and scheduled by the game workflow on the backend's task queue.
/// </summary>
public interface IGameProjectionActivities
{
    /// <summary>Persists the read model (save summary + journal) and pushes the view to connected browsers.</summary>
    /// <param name="projection">Latest view and unacknowledged journal entries.</param>
    /// <returns>A task that completes once the projection is durable.</returns>
    [Activity(ActivityNames.ProjectGame)]
    Task ProjectGameAsync(GameProjection projection);
}
