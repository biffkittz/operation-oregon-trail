using Oregon.Contracts.Model;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game.Rules;

/// <summary>Scoring and plausibility checks for the client-side click battle.</summary>
/// <remarks>
/// The browser reports its own score, so it can be forged. The workflow issued the spec (targets, duration),
/// so it can at least reject impossible reports; rewards are capped by the spec either way.
/// </remarks>
internal static class SkirmishRules
{
    private const int MinMillisecondsPerHit = 120;
    private const int MaxShotsPerTarget = 8;
    private const int OverrunGraceMilliseconds = 15_000;

    public static string? Validate(SkirmishSpecView spec, SkirmishResultCommand result)
    {
        if (result.Hits < 0 || result.ShotsFired < 0 || result.ElapsedMs < 0)
        {
            return "Negative numbers in a gunnery report? Nice try.";
        }

        if (result.Hits > spec.Targets)
        {
            return "You report more hits than drones were launched.";
        }

        if (result.Hits > result.ShotsFired)
        {
            return "You can't hit more drones than shots fired.";
        }

        if (result.ShotsFired > spec.Targets * MaxShotsPerTarget)
        {
            return "The cannon would have melted at that rate of fire.";
        }

        if (result.ElapsedMs > (spec.DurationSeconds * 1000) + OverrunGraceMilliseconds)
        {
            return "That battle ran longer than the raiders' patience.";
        }

        return result.ElapsedMs < result.Hits * MinMillisecondsPerHit ? "Even the best gunner needs more time than that." : null;
    }

    public static (Verdict Verdict, double Score) Score(SkirmishSpecView spec, SkirmishResultCommand result, bool hasGunner)
    {
        double accuracy = spec.Targets == 0 ? 0 : (double)result.Hits / spec.Targets;
        double precision = result.ShotsFired == 0 ? 0 : (double)result.Hits / result.ShotsFired;
        double score = Math.Clamp((0.7 * accuracy) + (0.3 * precision) + (hasGunner ? 0.1 : 0), 0, 1);
        Verdict verdict = score switch
        {
            >= 0.85 => Verdict.CriticalSuccess,
            >= 0.6 => Verdict.Success,
            >= 0.35 => Verdict.Partial,
            >= 0.15 => Verdict.Failure,
            _ => Verdict.CriticalFailure,
        };
        return (verdict, score);
    }

    public static Resolution Resolve(GameState state, ActiveSkirmish skirmish, SkirmishResultCommand result)
    {
        var profile = DifficultyProfile.For(state);
        (Verdict verdict, double score) = Score(skirmish.Spec, result, AllyRules.Has(state, AllyPerk.Gunner));
        List<ResourceChangeView> changes = [];

        // Rewards scale with performance: this is the "level up in proportion to how well you fought" loop.
        (ResourceDelta delta, int upgradePoints) = verdict switch
        {
            Verdict.CriticalSuccess => (new ResourceDelta(Scrip: 50, Morale: 6).Scale(profile.RewardMultiplier), 2),
            Verdict.Success => (new ResourceDelta(Scrip: 30, Morale: 3).Scale(profile.RewardMultiplier), 1),
            Verdict.Partial => (new ResourceDelta(Scrip: 10, Hull: -8), 0),
            Verdict.Failure => (new ResourceDelta(Hull: -16, Scrip: -15, Morale: -4).Scale(profile.PenaltyMultiplier), 0),
            _ => (new ResourceDelta(Hull: -24, Scrip: -25, Morale: -8).Scale(profile.PenaltyMultiplier), 0),
        };

        ResourceRules.Apply(state, delta, changes);
        ProgressionRules.GrantUpgradePoints(state, upgradePoints, changes);
        ProgressionRules.AddXp(state, StatKind.Reflexes, ProgressionRules.XpFor(verdict), changes);

        string summary = $"{skirmish.Title}: {result.Hits}/{skirmish.Spec.Targets} drones downed ({VerdictText.Describe(verdict)}, score {score:P0})";
        StoryFlags.Record(state, summary);
        return new Resolution(verdict, changes, null, summary);
    }
}
