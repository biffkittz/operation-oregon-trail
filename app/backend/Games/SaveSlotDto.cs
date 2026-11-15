namespace Oregon.Backend.Games;

/// <summary>One of the ten save slots; <paramref name="Game"/> is null when the slot is empty.</summary>
/// <param name="SlotNumber">Slot (1-10).</param>
/// <param name="Game">The save in this slot, if any.</param>
public sealed record SaveSlotDto(int SlotNumber, SavedGameSummary? Game);
