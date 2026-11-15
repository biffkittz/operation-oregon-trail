namespace Oregon.Worker.Game.State;

/// <summary>Why a challenge is being presented; decides what happens after it resolves.</summary>
internal enum ChallengeOrigin
{
    /// <summary>A random adversarial event mid-voyage; the ship arrives at its destination afterwards.</summary>
    TravelEvent,

    /// <summary>The encounter at the node the ship just docked at.</summary>
    NodeEncounter,

    /// <summary>Haggling with an outpost smith; the ship stays docked afterwards.</summary>
    Haggle,
}
