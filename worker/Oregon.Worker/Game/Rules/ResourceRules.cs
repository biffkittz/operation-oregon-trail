using Oregon.Contracts.Model;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game.Rules;

/// <summary>Applies resource deltas with clamping and records what actually changed for outcome cards.</summary>
internal static class ResourceRules
{
    public const int Max = 100;

    public static void Apply(GameState state, ResourceDelta delta, List<ResourceChangeView> changes)
    {
        ResourcePool pool = state.Resources;
        pool.Fuel = Track("Fuel", pool.Fuel, Math.Clamp(pool.Fuel + delta.Fuel, 0, Max), changes);
        pool.Rations = Track("Rations", pool.Rations, Math.Clamp(pool.Rations + delta.Rations, 0, Max), changes);
        pool.Hull = Track("Hull", pool.Hull, Math.Clamp(pool.Hull + delta.Hull, 0, Max), changes);
        pool.Morale = Track("Morale", pool.Morale, Math.Clamp(pool.Morale + delta.Morale, 0, Max), changes);
        pool.Scrip = Track("Scrip", pool.Scrip, Math.Max(0, pool.Scrip + delta.Scrip), changes);
        pool.Crew = Track("Crew", pool.Crew, Math.Max(0, pool.Crew + delta.Crew), changes);
    }

    /// <summary>Removes one crew member unless a Medic ally saves them.</summary>
    /// <returns>True when someone was actually lost.</returns>
    public static bool LoseCrewMember(GameState state, List<ResourceChangeView> changes)
    {
        if (AllyRules.TrySpendMedic(state))
        {
            changes.Add(new ResourceChangeView("Medic saved a life", 0));
            return false;
        }

        Apply(state, new ResourceDelta(Crew: -1), changes);
        return true;
    }

    private static int Track(string label, int before, int after, List<ResourceChangeView> changes)
    {
        if (after != before)
        {
            changes.Add(new ResourceChangeView(label, after - before));
        }

        return after;
    }
}
