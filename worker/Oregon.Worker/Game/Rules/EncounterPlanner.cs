using Oregon.Contracts.Model;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game.Rules;

/// <summary>Decides which encounter happens and what is at stake. Pure and deterministic given the Random.</summary>
internal static class EncounterPlanner
{
    private static readonly (string Situation, ResourceDelta Penalty)[] Hardships =
    [
        ("an ion squall overloads the shield grid and the bridge fills with acrid smoke", new ResourceDelta(Hull: -14)),
        ("a stowaway is caught siphoning the ration stores and pleads to stay aboard", new ResourceDelta(Rations: -14)),
        ("a coolant leak forces an evacuation of the aft habitat drum", new ResourceDelta(Hull: -10, Morale: -6)),
        ("a navigation fault drags the wagon toward a field of tumbling ice", new ResourceDelta(Hull: -12, Fuel: -6)),
        ("a fever spreads through the bunk ring and the crew turns on each other", new ResourceDelta(Morale: -12)),
        ("a micrometeor storm stitches holes across the cargo bay", new ResourceDelta(Hull: -16)),
        ("the fusion lamp sputters and the cabins drop below freezing", new ResourceDelta(Fuel: -8, Morale: -6)),
    ];

    private static readonly string[] DilemmaSituations =
    [
        "a plasma river cuts across the lane; its current surges with the pulse of a nearby star",
        "a gravel drift clogs the route with tumbling rock the size of houses",
        "a broken-down wagon train begs for tow cables at the edge of an ion storm",
        "a toll gate run by a self-appointed marshal blocks the only clear lane",
        "a magnetic shear twists the compass and splits the route into three uncertain paths",
        "a stampede of drifting ore haulers, their pilots long dead, barrels toward the ship",
    ];

    public static ChallengePlan Hardship(GameState state, Random random)
    {
        var profile = DifficultyProfile.For(state.Settings.Difficulty);
        (string situation, ResourceDelta penalty) = Hardships[random.Next(Hardships.Length)];
        return new ChallengePlan(
            ChallengeKind.Hardship,
            StatKind.Resilience,
            situation,
            Reward: new ResourceDelta(Morale: 4),
            Penalty: penalty,
            RewardUpgradePoints: 0,
            AllyOffer: null,
            Seconds: profile.HardshipSeconds);
    }

    public static ChallengePlan ForNode(GameState state, MapNode node, Random random)
    {
        var profile = DifficultyProfile.For(state.Settings.Difficulty);
        int seconds = profile.ChallengeSeconds;
        return node.Kind switch
        {
            NodeKind.Settlement when random.NextDouble() < 0.5 => new ChallengePlan(
                ChallengeKind.Persuasion,
                StatKind.Diplomacy,
                $"the homesteaders of {node.Name} have surplus food but distrust outsiders",
                Reward: new ResourceDelta(Rations: 22, Scrip: 15),
                Penalty: new ResourceDelta(Morale: -6),
                RewardUpgradePoints: 0,
                AllyOffer: MaybeAlly(state, random, 0.45),
                Seconds: seconds),
            NodeKind.Settlement => new ChallengePlan(
                ChallengeKind.Morality,
                StatKind.Morality,
                $"a settler at {node.Name} accuses one of your crew of theft and the colony demands justice",
                Reward: new ResourceDelta(Morale: 10, Rations: 8),
                Penalty: new ResourceDelta(Morale: -10),
                RewardUpgradePoints: 0,
                AllyOffer: MaybeAlly(state, random, 0.45),
                Seconds: seconds),
            NodeKind.Derelict => new ChallengePlan(
                ChallengeKind.Ingenuity,
                StatKind.Ingenuity,
                $"the cargo vault of the {node.Name} is sealed behind a failing reactor door",
                Reward: new ResourceDelta(Scrip: 35, Fuel: 12),
                Penalty: new ResourceDelta(Hull: -14),
                RewardUpgradePoints: 0,
                AllyOffer: null,
                Seconds: seconds),
            NodeKind.Anomaly when random.NextDouble() < 0.6 => new ChallengePlan(
                ChallengeKind.Creativity,
                StatKind.Creativity,
                $"{node.Name} responds to music, light and stories, and it seems to want a performance",
                Reward: new ResourceDelta(Morale: 12),
                Penalty: new ResourceDelta(Morale: -8),
                RewardUpgradePoints: 1,
                AllyOffer: null,
                Seconds: seconds),
            _ => new ChallengePlan(
                ChallengeKind.Morality,
                StatKind.Morality,
                $"{node.Name} offers to mend the ship's hull if one crew member agrees to stay behind forever",
                Reward: new ResourceDelta(Hull: 15, Morale: 6),
                Penalty: new ResourceDelta(Morale: -12),
                RewardUpgradePoints: 0,
                AllyOffer: null,
                Seconds: seconds),
        };
    }

    public static ChallengePlan Haggle(GameState state)
    {
        var profile = DifficultyProfile.For(state.Settings.Difficulty);
        OutpostState outpost = state.Outpost ?? throw new InvalidOperationException("Haggling requires an outpost.");
        return new ChallengePlan(
            ChallengeKind.Haggle,
            StatKind.Diplomacy,
            $"the outpost smith wants {outpost.RepairPrice} scrip to patch {outpost.RepairAmount}% of the hull and does not budge easily",
            Reward: default,
            Penalty: default,
            RewardUpgradePoints: 0,
            AllyOffer: null,
            Seconds: profile.ChallengeSeconds);
    }

    public static (string Situation, IReadOnlyList<DilemmaArchetype> Archetypes) Dilemma(Random random)
    {
        string situation = DilemmaSituations[random.Next(DilemmaSituations.Length)];
        List<DilemmaArchetype> pool = [.. Enum.GetValues<DilemmaArchetype>()];
        List<DilemmaArchetype> picked = new(3);
        while (picked.Count < 3)
        {
            int index = random.Next(pool.Count);
            picked.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return (situation, picked);
    }

    public static SkirmishSpecView Skirmish(GameState state, Random random)
    {
        var profile = DifficultyProfile.For(state.Settings.Difficulty);
        return new SkirmishSpecView(
            Seed: random.Next(1, int.MaxValue),
            Targets: profile.SkirmishTargets,
            DurationSeconds: profile.SkirmishSeconds,
            SpeedFactor: profile.SkirmishSpeed,
            TargetSizePx: profile.SkirmishTargetSize);
    }

    private static AllyState? MaybeAlly(GameState state, Random random, double chance)
    {
        double roll = random.NextDouble();
        return AllyRules.CanRecruit(state) && roll < chance ? AllyRules.CreateOffer(state, random, name: null) : null;
    }
}
