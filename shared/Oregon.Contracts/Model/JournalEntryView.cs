namespace Oregon.Contracts.Model;

/// <summary>One line of the captain's log.</summary>
/// <param name="Sequence">Monotonic sequence number within the save; used for idempotent persistence.</param>
/// <param name="Day">In-game day when the entry was written.</param>
/// <param name="Kind">Entry category.</param>
/// <param name="Text">Entry text.</param>
/// <param name="At">Wall-clock time (workflow time) of the entry.</param>
public sealed record JournalEntryView(long Sequence, int Day, JournalEntryKind Kind, string Text, DateTimeOffset At);
