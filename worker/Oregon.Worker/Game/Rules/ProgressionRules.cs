using Oregon.Contracts.Model;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game.Rules;

/// <summary>Experience, levels and upgrade points.</summary>
internal static class ProgressionRules
{
    public const int MaxLevel = 10;

    public static int XpToNext(int level) => level >= MaxLevel ? 0 : 30 + (15 * level);

    public static StatProgress Get(GameState state, StatKind stat) =>
        state.Stats.Find(s => s.Stat == stat) ?? throw new InvalidOperationException($"Stat {stat} is missing.");

    public static int Level(GameState state, StatKind stat) => Get(state, stat).Level;

    /// <summary>Level used for checks, including ally bonuses (an Envoy adds one to Diplomacy).</summary>
    public static int EffectiveLevel(GameState state, StatKind stat)
    {
        int bonus = stat == StatKind.Diplomacy && AllyRules.Has(state, AllyPerk.Envoy) ? 1 : 0;
        return Level(state, stat) + bonus;
    }

    /// <summary>Probability of passing a roll-based check at the given level (0.47 at level 2, 0.95 cap).</summary>
    public static double RollChance(int level) => Math.Clamp(0.35 + (0.06 * level), 0.05, 0.95);

    public static void AddXp(GameState state, StatKind stat, int amount, List<ResourceChangeView> changes)
    {
        StatProgress progress = Get(state, stat);
        if (amount <= 0 || progress.Level >= MaxLevel)
        {
            return;
        }

        progress.Xp += amount;
        changes.Add(new ResourceChangeView($"{stat} XP", amount));
        while (progress.Level < MaxLevel && progress.Xp >= XpToNext(progress.Level))
        {
            progress.Xp -= XpToNext(progress.Level);
            progress.Level++;
            changes.Add(new ResourceChangeView($"{stat} level", 1));
        }

        if (progress.Level >= MaxLevel)
        {
            progress.Xp = 0;
        }
    }

    public static void GrantUpgradePoints(GameState state, int points, List<ResourceChangeView> changes)
    {
        if (points <= 0)
        {
            return;
        }

        state.UpgradePoints += points;
        changes.Add(new ResourceChangeView("Upgrade points", points));
    }

    public static void SpendUpgrade(GameState state, StatKind stat, List<ResourceChangeView> changes)
    {
        StatProgress progress = Get(state, stat);
        state.UpgradePoints--;
        progress.Level++;
        progress.Xp = 0;
        changes.Add(new ResourceChangeView($"{stat} level", 1));
    }

    /// <summary>XP earned by the tested attribute for each verdict. Failures still teach something.</summary>
    public static int XpFor(Verdict verdict) => verdict switch
    {
        Verdict.CriticalSuccess => 40,
        Verdict.Success => 28,
        Verdict.Partial => 18,
        Verdict.Failure => 10,
        _ => 6,
    };
}
