using Oregon.Backend.Games;

namespace Oregon.Backend.Realtime;

/// <summary>Strongly typed SignalR client contract (methods the server invokes in the browser).</summary>
public interface IGameClient
{
    /// <summary>A save's state changed (player command, timer or deadline).</summary>
    /// <param name="snapshot">The new view.</param>
    /// <returns>A task.</returns>
    Task GameUpdated(GameSnapshotResponse snapshot);
}
