using Oregon.Contracts;
using Oregon.Contracts.Model;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game.Rules;

/// <summary>
/// Appends to the captain's log. Entries go both to the bounded recent list (embedded in every view) and to an
/// outbox that the projector drains into Postgres; the outbox survives Continue-As-New, so nothing is lost if
/// the backend is briefly unavailable.
/// </summary>
internal static class Journal
{
    /// <summary>Upper bound on unacknowledged entries; older ones are dropped rather than growing history forever.</summary>
    public const int MaxOutbox = 500;

    public static void Write(GameState state, JournalEntryKind kind, string? text, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        JournalEntryView entry = new(state.NextJournalSequence++, state.Day, kind, text.Trim(), new DateTimeOffset(nowUtc, TimeSpan.Zero));
        state.RecentJournal.Add(entry);
        if (state.RecentJournal.Count > GameLimits.RecentJournalSize)
        {
            state.RecentJournal.RemoveRange(0, state.RecentJournal.Count - GameLimits.RecentJournalSize);
        }

        state.JournalOutbox.Add(entry);
        if (state.JournalOutbox.Count > MaxOutbox)
        {
            state.JournalOutbox.RemoveRange(0, state.JournalOutbox.Count - MaxOutbox);
        }
    }

    public static void Acknowledge(GameState state, long upToSequence) =>
        state.JournalOutbox.RemoveAll(e => e.Sequence <= upToSequence);
}
