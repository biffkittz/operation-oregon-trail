using Oregon.Contracts.Model;

namespace Oregon.Backend.Games;

/// <summary>
/// A game view plus the server's clock, so the browser can correct for clock skew when rendering deadline
/// countdowns. Also the payload of the SignalR <c>GameUpdated</c> message.
/// </summary>
/// <param name="View">The view.</param>
/// <param name="ServerTime">Server time when the response was produced.</param>
/// <param name="Source">"live" (from the workflow) or "archive" (from the read model).</param>
public sealed record GameSnapshotResponse(GameView View, DateTimeOffset ServerTime, string Source);
