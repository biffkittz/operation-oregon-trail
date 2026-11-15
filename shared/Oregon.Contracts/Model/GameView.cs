namespace Oregon.Contracts.Model;

/// <summary>
/// Everything the UI needs to render a save. Built by the game workflow (query result, update result and
/// projection payload), so the frontend never reconstructs game state itself.
/// </summary>
public sealed record GameView
{
    /// <summary>Save identifier.</summary>
    public required Guid GameId { get; init; }

    /// <summary>Save slot (1-10).</summary>
    public required int SlotNumber { get; init; }

    /// <summary>Monotonic state version; clients ignore pushes older than what they already show.</summary>
    public required long Version { get; init; }

    /// <summary>Overall outcome.</summary>
    public required GameStatus Status { get; init; }

    /// <summary>What the workflow is waiting for.</summary>
    public required GamePhase Phase { get; init; }

    /// <summary>Voyage title.</summary>
    public required string Title { get; init; }

    /// <summary>Captain's name.</summary>
    public required string CaptainName { get; init; }

    /// <summary>Ship's name.</summary>
    public required string ShipName { get; init; }

    /// <summary>Narrative tone chosen at creation.</summary>
    public required GameTone Tone { get; init; }

    /// <summary>Difficulty chosen at creation.</summary>
    public required Difficulty Difficulty { get; init; }

    /// <summary>The story seed the voyage was generated from.</summary>
    public required string StorySeed { get; init; }

    /// <summary>In-game day.</summary>
    public required int Day { get; init; }

    /// <summary>Supplies and condition.</summary>
    public required ResourcesView Resources { get; init; }

    /// <summary>Captain attributes.</summary>
    public required IReadOnlyList<StatView> Stats { get; init; }

    /// <summary>Unspent upgrade points (earned mostly in skirmishes).</summary>
    public required int UpgradePoints { get; init; }

    /// <summary>Companions aboard.</summary>
    public required IReadOnlyList<AllyView> Allies { get; init; }

    /// <summary>Route map.</summary>
    public required MapView Map { get; init; }

    /// <summary>Most recent journal entries (older ones are paged from the read model).</summary>
    public required IReadOnlyList<JournalEntryView> Journal { get; init; }

    /// <summary>Available actions.</summary>
    public required PromptView Prompt { get; init; }

    /// <summary>Repairs in progress.</summary>
    public required IReadOnlyList<RepairJobView> RepairJobs { get; init; }

    /// <summary>Ending details once <see cref="Status"/> is not Active.</summary>
    public FinaleView? Finale { get; init; }

    /// <summary>Workflow time of the last state change.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
