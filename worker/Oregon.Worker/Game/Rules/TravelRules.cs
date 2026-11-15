using System.Globalization;
using Oregon.Contracts.Model;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game.Rules;

/// <summary>Costs and consequences of travelling a leg.</summary>
internal static class TravelRules
{
    /// <summary>Probability that a starving crew loses someone on a leg.</summary>
    private const double StarvationLossChance = 0.5;

    public static (int Fuel, int Rations) Cost(GameState state, int days)
    {
        var profile = DifficultyProfile.For(state);
        double fuelFactor = AllyRules.Has(state, AllyPerk.Navigator) ? 0.75 : 1.0;
        double rationFactor = AllyRules.Has(state, AllyPerk.Quartermaster) ? 0.75 : 1.0;
        int fuel = (int)Math.Ceiling(profile.FuelPerDay * days * fuelFactor);
        int rations = (int)Math.Ceiling(Math.Max(1, state.Resources.Crew) * days * rationFactor);
        return (fuel, rations);
    }

    public static MapEdge? FindRoute(GameState state, string nodeId) =>
        state.Map.EdgesFrom(state.Map.CurrentNodeId).FirstOrDefault(e => string.Equals(e.To, nodeId, StringComparison.Ordinal));

    public static IReadOnlyList<RouteOptionView> Routes(GameState state)
    {
        List<RouteOptionView> routes = [];
        foreach (MapEdge edge in state.Map.EdgesFrom(state.Map.CurrentNodeId))
        {
            MapNode? target = state.Map.FindNode(edge.To);
            if (target is null)
            {
                continue;
            }

            (int fuel, int rations) = Cost(state, edge.Days);
            bool affordable = state.Resources.Fuel >= fuel;
            string? warning = affordable ? StrandingWarning(state, target, state.Resources.Fuel - fuel) : null;
            routes.Add(new RouteOptionView(target.Id, target.Name, target.Kind, edge.Days, fuel, rations, affordable, warning));
        }

        return routes;
    }

    /// <summary>
    /// Warns when a destination has no fuel depot and the ship would arrive without the fuel for any route out.
    /// An estimate: events and ally perks on the way can change the arrival figure.
    /// </summary>
    private static string? StrandingWarning(GameState state, MapNode target, int arrivalFuel)
    {
        if (target.Kind is NodeKind.Outpost or NodeKind.Destination)
        {
            return null;
        }

        int[] onward = [.. state.Map.EdgesFrom(target.Id).Select(e => Cost(state, e.Days).Fuel)];
        if (onward.Length == 0 || arrivalFuel >= onward.Min())
        {
            return null;
        }

        string hauler = state.EmergencyFuelEnabled
            ? $" Only a passing hauler could sell you more: {EmergencyFuelRules.PriceMultiplier}x outpost prices, after days adrift."
            : string.Empty;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"No fuel depot at {target.Name}: you'd arrive with {arrivalFuel} fuel, and the shortest route on needs {onward.Min()}.{hauler}");
    }

    /// <summary>
    /// Consumes supplies for a leg. Every day in the black also wears the hull and the crew's spirits (the
    /// frontier is hard on wagons and people alike); running out of food is far worse.
    /// </summary>
    /// <returns>True when the crew went hungry.</returns>
    public static bool Travel(GameState state, MapEdge edge, Random random, List<ResourceChangeView> changes)
    {
        var profile = DifficultyProfile.For(state);
        (int fuel, int rations) = Cost(state, edge.Days);
        bool starving = state.Resources.Rations < rations;

        state.Day += edge.Days;
        changes.Add(new ResourceChangeView("Days", edge.Days));
        ResourceRules.Apply(
            state,
            new ResourceDelta(
                Fuel: -fuel,
                Rations: -rations,
                Hull: -profile.HullWearPerDay * edge.Days,
                Morale: -profile.MoraleDriftPerDay * edge.Days),
            changes);

        if (starving)
        {
            ResourceRules.Apply(state, new ResourceDelta(Morale: -8 * edge.Days), changes);
            if (random.NextDouble() < StarvationLossChance)
            {
                ResourceRules.LoseCrewMember(state, changes);
            }
        }
        else if (state.Resources.Rations < 15)
        {
            // Short rations: the crew worries even before the stores are empty.
            ResourceRules.Apply(state, new ResourceDelta(Morale: -2 * edge.Days), changes);
        }

        return starving;
    }

    public static bool RollTravelEvent(DifficultyProfile profile, Random random) => random.NextDouble() < profile.TravelEventChance;
}
