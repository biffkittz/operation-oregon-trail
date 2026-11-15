using Oregon.Contracts.Model;

namespace Oregon.Worker.Activities.Models;

/// <summary>Asks the LLM to dramatize a challenge the engine has already planned (kind, stat and stakes).</summary>
/// <param name="Context">Voyage context.</param>
/// <param name="Kind">Challenge kind.</param>
/// <param name="Stat">Attribute being tested.</param>
/// <param name="Situation">Engine-chosen premise, e.g. "an ion squall overwhelms the shields".</param>
/// <param name="StakesOnSuccess">What success earns.</param>
/// <param name="StakesOnFailure">What failure costs.</param>
public sealed record ChallengeRequest(
    StoryContext Context,
    ChallengeKind Kind,
    StatKind Stat,
    string Situation,
    string StakesOnSuccess,
    string StakesOnFailure);
