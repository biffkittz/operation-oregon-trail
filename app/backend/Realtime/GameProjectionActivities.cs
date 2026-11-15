using Microsoft.AspNetCore.SignalR;
using Oregon.Backend.Games;
using Oregon.Contracts;
using Oregon.Contracts.Workflows;
using Temporalio.Activities;

namespace Oregon.Backend.Realtime;

/// <summary>
/// Activity the game workflow schedules on this backend's task queue. Routing it here (instead of letting the
/// game worker write to Postgres) keeps the schema owned by one deployable and puts SignalR next to its clients.
/// </summary>
/// <remarks>Registered as a scoped activity, so each invocation gets a fresh DbContext.</remarks>
public sealed partial class GameProjectionActivities(
    ISaveSlotService slots,
    IHubContext<GameHub, IGameClient> hub,
    TimeProvider time,
    ILogger<GameProjectionActivities> logger) : IGameProjectionActivities
{
    /// <inheritdoc />
    [Activity(ActivityNames.ProjectGame)]
    public async Task ProjectGameAsync(GameProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        CancellationToken cancellationToken = ActivityExecutionContext.Current.CancellationToken;
        if (!await slots.ApplyProjectionAsync(projection, cancellationToken))
        {
            LogIgnored(logger, projection.View.GameId);
            return;
        }

        await hub.Clients.Group(GameHub.GroupName(projection.View.GameId))
            .GameUpdated(new GameSnapshotResponse(projection.View, time.GetUtcNow(), "live"));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ignoring projection for deleted game {GameId}")]
    private static partial void LogIgnored(ILogger logger, Guid gameId);
}
