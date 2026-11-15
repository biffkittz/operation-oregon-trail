namespace Oregon.Backend.Games;

/// <summary>Result of creating a game.</summary>
/// <param name="GameId">New save identifier.</param>
/// <param name="SlotNumber">Slot it occupies.</param>
public sealed record CreatedGameResponse(Guid GameId, int SlotNumber);
