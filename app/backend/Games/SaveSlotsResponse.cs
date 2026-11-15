namespace Oregon.Backend.Games;

/// <summary>All save slots.</summary>
/// <param name="Slots">Exactly <paramref name="MaxSlots"/> entries, ordered by slot number.</param>
/// <param name="MaxSlots">Slot count (also the cap on concurrently running game workflows).</param>
public sealed record SaveSlotsResponse(IReadOnlyList<SaveSlotDto> Slots, int MaxSlots);
