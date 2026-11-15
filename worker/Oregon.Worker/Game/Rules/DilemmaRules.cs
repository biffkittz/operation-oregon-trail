using Oregon.Contracts.Model;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game.Rules;

/// <summary>Mechanics of the three-way hazard choices.</summary>
internal static class DilemmaRules
{
    public static string Guidance(DilemmaArchetype archetype) => archetype switch
    {
        DilemmaArchetype.Bold => "A risky push straight through. Tests Resilience: success brings a windfall, failure damages the hull.",
        DilemmaArchetype.Cautious => "The safe, slow way. Costs an extra day and a day of food, but nobody gets hurt.",
        DilemmaArchetype.Compassionate => "Help someone else in trouble at a cost in food and scrip. Lifts morale; they may join the crew.",
        DilemmaArchetype.Mercantile => "Turn the situation into profit. Tests Diplomacy: success earns scrip, failure sours morale.",
        DilemmaArchetype.Inventive => "Rig a clever technical fix. Tests Ingenuity: success salvages fuel and hull, failure damages the hull.",
        _ => "An unorthodox, imaginative gambit. Tests Creativity: success inspires the crew, failure embarrasses them.",
    };

    public static string DefaultLabel(DilemmaArchetype archetype) => archetype switch
    {
        DilemmaArchetype.Bold => "Gun the engines and push straight through",
        DilemmaArchetype.Cautious => "Wait it out and take the long way around",
        DilemmaArchetype.Compassionate => "Stop to help the stranded travelers",
        DilemmaArchetype.Mercantile => "Strike a deal with whoever controls the lane",
        DilemmaArchetype.Inventive => "Jury-rig the deflectors and thread the gap",
        _ => "Try something nobody has tried before",
    };

    public static string DefaultHint(DilemmaArchetype archetype) => archetype switch
    {
        DilemmaArchetype.Bold => "Fortune favors the bold. Usually.",
        DilemmaArchetype.Cautious => "Safe, slow and hungry.",
        DilemmaArchetype.Compassionate => "Kindness costs supplies.",
        DilemmaArchetype.Mercantile => "Money talks, if you can.",
        DilemmaArchetype.Inventive => "Clever hands required.",
        _ => "Wild, and it might just work.",
    };

    public static StatKind? TestedStat(DilemmaArchetype archetype) => archetype switch
    {
        DilemmaArchetype.Bold => StatKind.Resilience,
        DilemmaArchetype.Mercantile => StatKind.Diplomacy,
        DilemmaArchetype.Inventive => StatKind.Ingenuity,
        DilemmaArchetype.Daring => StatKind.Creativity,
        _ => null,
    };

    public static Resolution Resolve(GameState state, ActiveDilemma dilemma, DilemmaOption option, Random random)
    {
        var profile = DifficultyProfile.For(state.Settings.Difficulty);
        List<ResourceChangeView> changes = [];
        AllyState? joined = null;
        Verdict verdict;

        switch (option.Archetype)
        {
            case DilemmaArchetype.Cautious:
                verdict = Verdict.Success;
                state.Day += 1;
                changes.Add(new ResourceChangeView("Days", 1));
                ResourceRules.Apply(state, new ResourceDelta(Rations: -Math.Max(1, state.Resources.Crew), Morale: 2), changes);
                break;

            case DilemmaArchetype.Compassionate:
                verdict = Verdict.Success;
                ResourceRules.Apply(state, new ResourceDelta(Rations: -10, Scrip: -10, Morale: 8), changes);
                ProgressionRules.AddXp(state, StatKind.Morality, 25, changes);
                if (AllyRules.CanRecruit(state) && random.NextDouble() < 0.4)
                {
                    joined = AllyRules.CreateOffer(state, random, name: null);
                    AllyRules.Join(state, joined, changes);
                }

                break;

            default:
                StatKind stat = TestedStat(option.Archetype) ?? StatKind.Resilience;
                verdict = Roll(ProgressionRules.EffectiveLevel(state, stat), random);
                bool success = verdict is Verdict.CriticalSuccess or Verdict.Success or Verdict.Partial;
                (ResourceDelta onSuccess, ResourceDelta onFailure, int upgrade) = Effects(option.Archetype);
                ResourceDelta delta = success
                    ? onSuccess.Scale(profile.RewardMultiplier * (verdict == Verdict.CriticalSuccess ? 1.5 : verdict == Verdict.Partial ? 0.5 : 1.0))
                    : onFailure.Scale(profile.PenaltyMultiplier * (verdict == Verdict.CriticalFailure ? 1.5 : 1.0));
                ResourceRules.Apply(state, delta, changes);
                ProgressionRules.AddXp(state, stat, ProgressionRules.XpFor(verdict), changes);
                if (success && verdict != Verdict.Partial)
                {
                    ProgressionRules.GrantUpgradePoints(state, upgrade, changes);
                }

                break;
        }

        string summary = $"{dilemma.Title}: chose to {option.Label.ToLowerInvariant()} ({VerdictText.Describe(verdict)})";
        StoryFlags.Record(state, summary);
        return new Resolution(verdict, changes, joined, summary);
    }

    /// <summary>The hazard decides for a captain who hesitates past the deadline.</summary>
    public static Resolution ResolveTimeout(GameState state, ActiveDilemma dilemma)
    {
        var profile = DifficultyProfile.For(state.Settings.Difficulty);
        List<ResourceChangeView> changes = [];
        ResourceRules.Apply(state, new ResourceDelta(Hull: -12, Morale: -6).Scale(profile.PenaltyMultiplier), changes);
        string summary = $"{dilemma.Title}: hesitated and the hazard decided";
        StoryFlags.Record(state, summary);
        return new Resolution(Verdict.Failure, changes, null, summary);
    }

    private static (ResourceDelta OnSuccess, ResourceDelta OnFailure, int UpgradePoints) Effects(DilemmaArchetype archetype) => archetype switch
    {
        DilemmaArchetype.Bold => (new ResourceDelta(Scrip: 20, Morale: 5), new ResourceDelta(Hull: -15), 0),
        DilemmaArchetype.Mercantile => (new ResourceDelta(Scrip: 35), new ResourceDelta(Morale: -10), 0),
        DilemmaArchetype.Inventive => (new ResourceDelta(Fuel: 15, Hull: 10), new ResourceDelta(Hull: -10), 0),
        _ => (new ResourceDelta(Morale: 8), new ResourceDelta(Morale: -8), 1),
    };

    private static Verdict Roll(int level, Random random)
    {
        double chance = ProgressionRules.RollChance(level);
        double roll = random.NextDouble();
        if (roll < chance * 0.25)
        {
            return Verdict.CriticalSuccess;
        }

        if (roll < chance * 0.8)
        {
            return Verdict.Success;
        }

        if (roll < chance)
        {
            return Verdict.Partial;
        }

        return roll > 1 - ((1 - chance) * 0.25) ? Verdict.CriticalFailure : Verdict.Failure;
    }
}
