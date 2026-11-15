using Oregon.Contracts.Model;

namespace Oregon.Backend.Data;

/// <summary>One persisted line of a save's captain's log. Unique on (GameId, Sequence) for idempotent writes.</summary>
public sealed class JournalEntryRecord
{
    /// <summary>Gets or sets the surrogate key.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the owning save.</summary>
    public Guid GameId { get; set; }

    /// <summary>Gets or sets the workflow-assigned sequence number (monotonic per save).</summary>
    public long Sequence { get; set; }

    /// <summary>Gets or sets the in-game day of the entry.</summary>
    public int Day { get; set; }

    /// <summary>Gets or sets the entry category.</summary>
    public JournalEntryKind Kind { get; set; }

    /// <summary>Gets or sets the entry text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Gets or sets the workflow time of the entry.</summary>
    public DateTimeOffset At { get; set; }
}
