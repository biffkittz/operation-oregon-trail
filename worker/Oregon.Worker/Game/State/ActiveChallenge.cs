using Oregon.Contracts.Model;

namespace Oregon.Worker.Game.State;

/// <summary>A free-text challenge awaiting the player's response.</summary>
internal sealed class ActiveChallenge
{
    public ChallengeKind Kind { get; set; }

    public StatKind Stat { get; set; }

    public ChallengeOrigin Origin { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? NpcName { get; set; }

    public string? NpcLine { get; set; }

    public string StakesOnSuccess { get; set; } = string.Empty;

    public string StakesOnFailure { get; set; } = string.Empty;

    public ResourceDelta Reward { get; set; }

    public ResourceDelta Penalty { get; set; }

    public int RewardUpgradePoints { get; set; }

    /// <summary>Gets or sets the ally who joins on success, if this encounter offers one.</summary>
    public AllyState? AllyOffer { get; set; }

    /// <summary>Gets or sets the node the ship was travelling to when a travel event interrupted it.</summary>
    public string? PendingNodeId { get; set; }

    public DateTime? DeadlineUtc { get; set; }
}
