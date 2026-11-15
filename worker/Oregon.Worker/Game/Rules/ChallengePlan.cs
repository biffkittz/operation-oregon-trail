using Oregon.Contracts.Model;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game.Rules;

/// <summary>
/// The engine's decision about a challenge: what is tested and what is at stake. The LLM only dramatizes it,
/// which keeps rewards bounded no matter what the model writes.
/// </summary>
internal sealed record ChallengePlan(
    ChallengeKind Kind,
    StatKind Stat,
    string Situation,
    ResourceDelta Reward,
    ResourceDelta Penalty,
    int RewardUpgradePoints,
    AllyState? AllyOffer,
    int? Seconds);
