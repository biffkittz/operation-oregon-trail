using System.Text.Json.Serialization;

namespace Oregon.Worker.Game.State;

/// <summary>A bundle of signed resource changes, used for stakes, rewards and penalties.</summary>
internal readonly record struct ResourceDelta(
    int Fuel = 0,
    int Rations = 0,
    int Hull = 0,
    int Morale = 0,
    int Scrip = 0,
    int Crew = 0)
{
    [JsonIgnore]
    public bool IsEmpty => Fuel == 0 && Rations == 0 && Hull == 0 && Morale == 0 && Scrip == 0 && Crew == 0;

    /// <summary>Scales every component, rounding away from zero so small stakes never vanish.</summary>
    public ResourceDelta Scale(double factor) => new(
        ScaleOne(Fuel, factor),
        ScaleOne(Rations, factor),
        ScaleOne(Hull, factor),
        ScaleOne(Morale, factor),
        ScaleOne(Scrip, factor),
        Crew);

    public static ResourceDelta operator +(ResourceDelta left, ResourceDelta right) => new(
        left.Fuel + right.Fuel,
        left.Rations + right.Rations,
        left.Hull + right.Hull,
        left.Morale + right.Morale,
        left.Scrip + right.Scrip,
        left.Crew + right.Crew);

    private static int ScaleOne(int value, double factor) =>
        value == 0 ? 0 : (int)Math.Round(value * factor, MidpointRounding.AwayFromZero);
}
