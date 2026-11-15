namespace Oregon.Backend.Games;

/// <summary>Body of a 202: the command is still running and its result will be pushed over SignalR.</summary>
/// <param name="Message">Player-facing message.</param>
public sealed record CommandPendingResponse(string Message);
