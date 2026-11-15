using System.Globalization;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game.Rules;

/// <summary>Formats resource deltas as plain language for the LLM prompt and the challenge card.</summary>
internal static class StakesText
{
    public static string Describe(ResourceDelta delta, int upgradePoints = 0, AllyState? ally = null)
    {
        List<string> parts = [];
        Add(parts, delta.Fuel, "fuel");
        Add(parts, delta.Rations, "rations");
        Add(parts, delta.Hull, "hull");
        Add(parts, delta.Morale, "morale");
        Add(parts, delta.Scrip, "scrip");
        Add(parts, delta.Crew, "crew");
        Add(parts, upgradePoints, upgradePoints == 1 ? "upgrade point" : "upgrade points");
        if (ally is not null)
        {
            parts.Add($"{ally.Name} may join the crew");
        }

        return parts.Count == 0 ? "pride and nothing else" : string.Join(", ", parts);
    }

    private static void Add(List<string> parts, int value, string label)
    {
        if (value != 0)
        {
            parts.Add($"{value.ToString("+0;-0", CultureInfo.InvariantCulture)} {label}");
        }
    }
}
