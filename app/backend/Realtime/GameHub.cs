using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Oregon.Backend.Realtime;

/// <summary>
/// Pushes game updates to the browser. Clients join a per-game group while the game screen is open. A single
/// app replica means no SignalR backplane is needed; scaling out would require Azure SignalR or Redis.
/// </summary>
[Authorize]
public sealed class GameHub : Hub<IGameClient>
{
    /// <summary>Route the hub is mapped to.</summary>
    public const string Path = "/hubs/game";

    /// <summary>Group name for one save.</summary>
    /// <param name="gameId">Save identifier.</param>
    /// <returns>Group name.</returns>
    public static string GroupName(Guid gameId) => $"game-{gameId:N}";

    /// <summary>Subscribe to a save's updates.</summary>
    /// <param name="gameId">Save identifier.</param>
    /// <returns>A task.</returns>
    public Task WatchGame(Guid gameId) => Groups.AddToGroupAsync(Context.ConnectionId, GroupName(gameId));

    /// <summary>Unsubscribe from a save's updates.</summary>
    /// <param name="gameId">Save identifier.</param>
    /// <returns>A task.</returns>
    public Task UnwatchGame(Guid gameId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(gameId));
}
