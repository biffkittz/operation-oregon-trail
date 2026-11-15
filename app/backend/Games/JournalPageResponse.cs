using Oregon.Contracts.Model;

namespace Oregon.Backend.Games;

/// <summary>A page of older journal entries, oldest first.</summary>
/// <param name="Entries">Entries.</param>
/// <param name="HasMore">Whether even older entries exist.</param>
public sealed record JournalPageResponse(IReadOnlyList<JournalEntryView> Entries, bool HasMore);
