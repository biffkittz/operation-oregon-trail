using Oregon.Contracts.Model;
using Oregon.Worker.Activities.Models;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game;

/// <summary>Builds the compact voyage summary sent with every LLM request.</summary>
internal static class StoryContextFactory
{
    private const int RecentEventCount = 6;
    private const int MaxEventLength = 220;

    public static StoryContext Create(GameState state)
    {
        MapNode? node = state.Map.FindNode(state.Map.CurrentNodeId);
        List<string> recent = [.. state.RecentJournal
            .Where(e => e.Kind is JournalEntryKind.Narration or JournalEntryKind.Outcome or JournalEntryKind.Event or JournalEntryKind.Npc)
            .TakeLast(RecentEventCount)
            .Select(e => e.Text.Length > MaxEventLength ? $"{e.Text[..MaxEventLength]}..." : e.Text)];

        return new StoryContext(
            state.Settings.Title,
            state.Settings.StorySeed,
            state.Settings.Tone,
            state.Settings.Difficulty,
            state.Settings.CaptainName,
            state.Settings.ShipName,
            state.Day,
            node?.Name ?? "the open void",
            node?.Description ?? string.Empty,
            state.Resources.ToView(),
            [.. state.Allies.Select(a => $"{a.Name} ({a.Role})")],
            [.. state.StoryFlags],
            recent);
    }
}
