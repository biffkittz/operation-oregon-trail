using Oregon.Contracts.Model;

namespace Oregon.Backend.Data;

/// <summary>
/// One occupied save slot: identity, a denormalized summary for the slot list, and the last projected view
/// (served when the workflow cannot be queried, e.g. long after the voyage ended).
/// </summary>
public sealed class GameRecord
{
    /// <summary>Gets or sets the save identifier (also embedded in the Temporal workflow ID).</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the slot (1-10). A unique index makes the 10-game cap race-free.</summary>
    public int SlotNumber { get; set; }

    /// <summary>Gets or sets the Temporal workflow ID that owns this save's live state.</summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>Gets or sets the voyage title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Gets or sets the captain's name.</summary>
    public string CaptainName { get; set; } = string.Empty;

    /// <summary>Gets or sets the ship's name.</summary>
    public string ShipName { get; set; } = string.Empty;

    /// <summary>Gets or sets the tone chosen at creation.</summary>
    public GameTone Tone { get; set; }

    /// <summary>Gets or sets the difficulty chosen at creation.</summary>
    public Difficulty Difficulty { get; set; }

    /// <summary>Gets or sets the overall outcome.</summary>
    public GameStatus Status { get; set; }

    /// <summary>Gets or sets the phase at the last projection.</summary>
    public GamePhase Phase { get; set; }

    /// <summary>Gets or sets the in-game day.</summary>
    public int Day { get; set; }

    /// <summary>Gets or sets the hull percentage.</summary>
    public int Hull { get; set; }

    /// <summary>Gets or sets the crew count.</summary>
    public int Crew { get; set; }

    /// <summary>Gets or sets where the ship is docked.</summary>
    public string LocationName { get; set; } = string.Empty;

    /// <summary>Gets or sets the final score once the voyage has ended.</summary>
    public int? Score { get; set; }

    /// <summary>Gets or sets the version of the last projection applied (older projections are ignored).</summary>
    public long StateVersion { get; set; }

    /// <summary>Gets or sets the last projected <see cref="GameView"/> as JSON (jsonb).</summary>
    public string? LastViewJson { get; set; }

    /// <summary>Gets or sets when the voyage began.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets when the read model last changed.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Gets or sets the persisted journal (navigation property).</summary>
    public List<JournalEntryRecord> Journal { get; set; } = [];
}
