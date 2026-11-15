namespace Oregon.Worker.Game.State;

/// <summary>A three-way choice awaiting the player.</summary>
internal sealed class ActiveDilemma
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<DilemmaOption> Options { get; set; } = [];

    public DateTime? DeadlineUtc { get; set; }
}
