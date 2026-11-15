namespace Oregon.Worker.Game.State;

/// <summary>One option of an active dilemma.</summary>
internal sealed class DilemmaOption
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Hint { get; set; } = string.Empty;

    public DilemmaArchetype Archetype { get; set; }
}
