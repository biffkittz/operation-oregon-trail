using System.Globalization;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game.Rules;

/// <summary>Defeat conditions and scoring.</summary>
internal static class EndgameRules
{
    public static string? DefeatReason(GameState state)
    {
        if (state.Resources.Hull <= 0)
        {
            return "The hull gave out and the void came in.";
        }

        if (state.Resources.Crew <= 0)
        {
            return "No one is left aboard to fly the wagon.";
        }

        return state.Resources.Morale <= 0 ? "The crew mutinied and turned the wagon back toward home." : null;
    }

    /// <summary>True when no route is affordable and no fuel can be bought here.</summary>
    public static bool IsStranded(GameState state)
    {
        if (TravelRules.Routes(state).Any(r => r.Affordable))
        {
            return false;
        }

        return state.Outpost is { } outpost
            ? state.Resources.Scrip < outpost.FuelPrice
            : EmergencyFuelRules.Offer(state) is null;
    }

    /// <summary>
    /// The specific reason a stranded ship lost, with the numbers that caused it. The epilogue narrator only gets
    /// these facts, so a vague reason ("no way to buy more") invites it to invent one ("no scrip left").
    /// </summary>
    public static string StrandedReason(GameState state)
    {
        ResourcePool r = state.Resources;
        string place = state.Map.FindNode(state.Map.CurrentNodeId)?.Name ?? "this stop";
        int cheapest = TravelRules.Routes(state).Select(route => route.FuelCost).DefaultIfEmpty(0).Min();
        string tank = string.Create(CultureInfo.InvariantCulture,
            $"The tank holds {r.Fuel} fuel, but the shortest route out of {place} needs {cheapest}");

        if (state.Outpost is { } outpost)
        {
            return string.Create(CultureInfo.InvariantCulture,
                $"{tank}, and fuel here costs {outpost.FuelPrice} scrip with only {r.Scrip} scrip left. The wagon drifts to a stop.");
        }

        return EmergencyFuelRules.Quote(state) is { } quote
            ? string.Create(CultureInfo.InvariantCulture,
                $"{tank}, and {place} has no fuel depot. A passing hauler wanted {quote.Price} scrip for {quote.Fuel} fuel; only {r.Scrip} scrip remain. The wagon drifts to a stop.")
            : string.Create(CultureInfo.InvariantCulture,
                $"{tank}, and {place} has no fuel depot; the {r.Scrip} scrip aboard can't buy what isn't for sale. The wagon drifts to a stop.");
    }

    public static int Score(GameState state, bool victory)
    {
        int levels = state.Stats.Sum(s => s.Level);
        ResourcePool r = state.Resources;
        if (!victory)
        {
            return Math.Max(0, (state.LegsCompleted * 60) + (levels * 10) + (state.Allies.Count * 25));
        }

        int score = 1000 + (levels * 15) + (r.Hull * 2) + r.Fuel + r.Rations + (r.Scrip / 2)
            + (state.Allies.Count * 60) + (r.Crew * 40) - (state.Day * 10);
        return Math.Max(100, score);
    }
}
