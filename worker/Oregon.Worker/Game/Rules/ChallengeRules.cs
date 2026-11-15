using Oregon.Contracts.Model;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game.Rules;

/// <summary>Turns a judge's verdict into bounded, deterministic consequences.</summary>
internal static class ChallengeRules
{
    public static int MoraleFor(Verdict verdict) => verdict switch
    {
        Verdict.CriticalSuccess => 6,
        Verdict.Success => 3,
        Verdict.Partial => 0,
        Verdict.Failure => -3,
        _ => -6,
    };

    public static Resolution Resolve(GameState state, ActiveChallenge challenge, Verdict verdict)
    {
        var profile = DifficultyProfile.For(state.Settings.Difficulty);
        List<ResourceChangeView> changes = [];
        ProgressionRules.AddXp(state, challenge.Stat, ProgressionRules.XpFor(verdict), changes);

        ResourceDelta delta = new(Morale: MoraleFor(verdict));
        if (challenge.Kind == ChallengeKind.Haggle)
        {
            OutpostRules.ApplyHaggle(state, verdict, changes);
        }
        else
        {
            ResourceDelta reward = challenge.Reward.Scale(profile.RewardMultiplier);
            ResourceDelta penalty = challenge.Penalty.Scale(profile.PenaltyMultiplier);
            delta += verdict switch
            {
                Verdict.CriticalSuccess => reward.Scale(1.5),
                Verdict.Success => reward,
                Verdict.Partial => reward.Scale(0.5) + penalty.Scale(0.5),
                Verdict.Failure => penalty,
                _ => penalty.Scale(1.5),
            };

            if (verdict is Verdict.CriticalSuccess or Verdict.Success && challenge.RewardUpgradePoints > 0)
            {
                int bonus = verdict == Verdict.CriticalSuccess ? 1 : 0;
                ProgressionRules.GrantUpgradePoints(state, challenge.RewardUpgradePoints + bonus, changes);
            }
        }

        ResourceRules.Apply(state, delta, changes);

        AllyState? joined = null;
        if (challenge.AllyOffer is not null && verdict is Verdict.CriticalSuccess or Verdict.Success && AllyRules.CanRecruit(state))
        {
            joined = challenge.AllyOffer;
            AllyRules.Join(state, joined, changes);
        }

        string summary = $"{challenge.Title}: {VerdictText.Describe(verdict)}";
        StoryFlags.Record(state, summary);
        return new Resolution(verdict, changes, joined, summary);
    }

    /// <summary>Consequences when the captain lets the deadline pass.</summary>
    public static Resolution ResolveTimeout(GameState state, ActiveChallenge challenge) =>
        Resolve(state, challenge, challenge.Kind == ChallengeKind.Hardship ? Verdict.CriticalFailure : Verdict.Failure);
}
