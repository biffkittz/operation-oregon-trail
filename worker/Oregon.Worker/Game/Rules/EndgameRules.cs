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

        return state.Outpost is not { } outpost || state.Resources.Scrip < outpost.FuelPrice;
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
