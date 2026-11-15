using Oregon.Contracts.Model;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game.Rules;

/// <summary>Ally creation, perks and limits.</summary>
internal static class AllyRules
{
    public const int MaxAllies = 4;

    private static readonly (string Role, AllyPerk Perk)[] Roles =
    [
        ("Hull mechanic", AllyPerk.Mechanic),
        ("Quartermaster", AllyPerk.Quartermaster),
        ("Navigator", AllyPerk.Navigator),
        ("Field medic", AllyPerk.Medic),
        ("Envoy", AllyPerk.Envoy),
        ("Gunner", AllyPerk.Gunner),
    ];

    public static bool Has(GameState state, AllyPerk perk) =>
        state.Allies.Exists(a => a.Perk == perk && !a.PerkSpent);

    public static string Describe(AllyPerk perk) => perk switch
    {
        AllyPerk.Mechanic => "Patches 4 hull after every leg",
        AllyPerk.Quartermaster => "Crew eats 25% less",
        AllyPerk.Navigator => "Ship burns 25% less fuel",
        AllyPerk.Medic => "Saves the next crew member who would be lost",
        AllyPerk.Envoy => "+1 level on Diplomacy checks",
        AllyPerk.Gunner => "Steadier aim in skirmishes",
        _ => "Good company",
    };

    /// <summary>Creates a prospective ally, preferring perks the crew does not have yet.</summary>
    public static AllyState CreateOffer(GameState state, Random random, string? name)
    {
        List<(string Role, AllyPerk Perk)> candidates = [.. Roles.Where(r => !state.Allies.Exists(a => a.Perk == r.Perk))];
        if (candidates.Count == 0)
        {
            candidates = [.. Roles];
        }

        (string role, AllyPerk perk) = candidates[random.Next(candidates.Count)];
        return new AllyState
        {
            Name = string.IsNullOrWhiteSpace(name) ? NameBank.AllyName(random) : name.Trim(),
            Role = role,
            Perk = perk,
        };
    }

    public static bool CanRecruit(GameState state) => state.Allies.Count < MaxAllies;

    public static void Join(GameState state, AllyState ally, List<ResourceChangeView> changes)
    {
        state.Allies.Add(ally);
        ResourceRules.Apply(state, new ResourceDelta(Crew: 1), changes);
        changes.Add(new ResourceChangeView($"Ally: {ally.Name}", 1));
    }

    /// <summary>Per-leg perks (applied on arrival at each node).</summary>
    public static void ApplyLegPerks(GameState state, List<ResourceChangeView> changes)
    {
        if (Has(state, AllyPerk.Mechanic))
        {
            ResourceRules.Apply(state, new ResourceDelta(Hull: 4), changes);
        }
    }

    public static bool TrySpendMedic(GameState state)
    {
        AllyState? medic = state.Allies.Find(a => a.Perk == AllyPerk.Medic && !a.PerkSpent);
        if (medic is null)
        {
            return false;
        }

        medic.PerkSpent = true;
        return true;
    }

    public static AllyView ToView(AllyState ally) =>
        new(ally.Name, ally.Role, ally.PerkSpent ? $"{Describe(ally.Perk)} (used)" : Describe(ally.Perk));
}
