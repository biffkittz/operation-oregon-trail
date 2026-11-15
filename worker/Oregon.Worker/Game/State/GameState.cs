using Oregon.Contracts;
using Oregon.Contracts.Model;
using Oregon.Contracts.Workflows;

namespace Oregon.Worker.Game.State;

/// <summary>
/// The complete, serializable state of one voyage. The game workflow owns exactly one instance and carries it
/// across Continue-As-New inside <see cref="GameContinuation"/>.
/// </summary>
/// <remarks>
/// This is deliberately a plain mutable class: game rules mutate it in place and it must round-trip through
/// System.Text.Json. Bump <see cref="CurrentSchemaVersion"/> (and add an upgrade step in
/// <see cref="GameStateSerializer"/>) whenever a change would break deserialization of older saves.
/// </remarks>
internal sealed class GameState
{
    /// <summary>Schema version written by this build.</summary>
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public Guid GameId { get; set; }

    public int SlotNumber { get; set; }

    public NewGameSettings Settings { get; set; } = new("Untitled", "Captain", "Starwagon", GameTone.Gritty, Difficulty.Normal, string.Empty);

    public string ProjectionTaskQueue { get; set; } = TaskQueues.Web;

    /// <summary>Gets or sets the monotonic version; incremented on every state change.</summary>
    public long Version { get; set; }

    public GamePhase Phase { get; set; } = GamePhase.Initializing;

    public GameStatus Status { get; set; } = GameStatus.Active;

    public int Day { get; set; } = 1;

    public ResourcePool Resources { get; set; } = new();

    public List<StatProgress> Stats { get; set; } = [];

    public int UpgradePoints { get; set; }

    public List<AllyState> Allies { get; set; } = [];

    public WorldMap Map { get; set; } = new();

    /// <summary>Gets or sets short story facts (e.g. "rescued_miners") fed back to the LLM to branch the narrative.</summary>
    public List<string> StoryFlags { get; set; } = [];

    /// <summary>Gets or sets the most recent journal entries shown in the view.</summary>
    public List<JournalEntryView> RecentJournal { get; set; } = [];

    /// <summary>Gets or sets journal entries not yet acknowledged by the read model (transactional outbox).</summary>
    public List<JournalEntryView> JournalOutbox { get; set; } = [];

    public long NextJournalSequence { get; set; } = 1;

    public ActiveChallenge? Challenge { get; set; }

    public ActiveDilemma? Dilemma { get; set; }

    public ActiveSkirmish? Skirmish { get; set; }

    public PendingNarrative? Narrative { get; set; }

    public OutpostState? Outpost { get; set; }

    public List<RepairJob> RepairJobs { get; set; } = [];

    public int NextJobNumber { get; set; } = 1;

    public FinaleView? Finale { get; set; }

    public int LegsCompleted { get; set; }

    public int TurnCount { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
