namespace Oregon.Worker.Game.State;

/// <summary>A companion aboard the ship.</summary>
internal sealed class AllyState
{
    public string Name { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public AllyPerk Perk { get; set; }

    /// <summary>Gets or sets a value indicating whether a one-shot perk (Medic) has been spent.</summary>
    public bool PerkSpent { get; set; }
}
