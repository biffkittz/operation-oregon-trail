using Oregon.Contracts.Model;

namespace Oregon.Worker.Game.Rules;

/// <summary>
/// Deterministic, original placeholder names. Used for the map before the LLM renames it, and as fallback
/// content when the LLM is unavailable. Some landmarks borrow real Oregon Trail geography (public place names),
/// never names from any game or franchise.
/// </summary>
internal static class NameBank
{
    public const string OriginName = "Independence Ring";
    public const string DestinationName = "Willamette Landing";

    private static readonly string[] OutpostFirst = ["Rustwater", "Kettle Point", "Cinder", "Brass Hollow", "Saltlick", "Lantern", "Dry Creek", "Copperline", "Juniper", "Tallow"];
    private static readonly string[] OutpostSecond = ["Depot", "Waystation", "Trading Post", "Supply Dock", "Exchange"];
    private static readonly string[] SettlementFirst = ["Hope's Ledge", "New Galena", "Ashfall", "Prosper", "Little Canaan", "Sweetwater", "Bitterroot", "Harmony", "Gilead", "Fairhaven"];
    private static readonly string[] SettlementSecond = ["Homestead", "Colony", "Camp", "Commune", "Settlement"];
    private static readonly string[] DerelictShips = ["Covenant", "Lucky Strike", "Iron Bonnet", "Meridian", "Dutiful", "Starling", "Plainsong", "Ox and Anchor"];
    private static readonly string[] HazardFirst = ["Ion Squall", "Plasma River", "Gravel Drift", "Static Gorge", "Cold Stretch", "Dust Devil", "Shear Current"];
    private static readonly string[] HazardSecond = ["Narrows", "Crossing", "Flats", "Shoals", "Pass"];
    private static readonly string[] RaiderFirst = ["Claim-Jumper", "Rustler", "Scavenger", "Outlaw", "Bushwhacker"];
    private static readonly string[] RaiderSecond = ["Hideout", "Roost", "Gulch", "Run", "Den"];
    private static readonly string[] AnomalyFirst = ["Singing", "Hollow", "Mirror", "Weeping", "Silent", "Laughing"];
    private static readonly string[] AnomalySecond = ["Nebula", "Lights", "Pulsar", "Rift", "Bloom"];
    private static readonly string[] Landmarks = ["Chimney Spire", "Courthouse Nebula", "Register Cliffs", "The Lone Beacon", "Soda Springs Halo", "Devil's Gate Pulsar", "Ash Hollow Ring"];
    private static readonly string[] AllyFirst = ["Mae", "Ezra", "Tobias", "Ione", "Rufus", "Clementine", "Silas", "Odessa", "Jubal", "Wren", "Hester", "Amos"];
    private static readonly string[] AllyLast = ["Calloway", "Thorne", "Pike", "Whitlock", "Bramble", "Quill", "Hargrove", "Delacroix", "Finch", "Oakes"];

    public static string PlaceholderName(NodeKind kind, Random random) => kind switch
    {
        NodeKind.Origin => OriginName,
        NodeKind.Destination => DestinationName,
        NodeKind.Outpost => $"{Pick(OutpostFirst, random)} {Pick(OutpostSecond, random)}",
        NodeKind.Settlement => $"{Pick(SettlementFirst, random)} {Pick(SettlementSecond, random)}",
        NodeKind.Derelict => $"Wreck of the {Pick(DerelictShips, random)}",
        NodeKind.Hazard => $"{Pick(HazardFirst, random)} {Pick(HazardSecond, random)}",
        NodeKind.Raiders => $"{Pick(RaiderFirst, random)} {Pick(RaiderSecond, random)}",
        NodeKind.Anomaly => $"The {Pick(AnomalyFirst, random)} {Pick(AnomalySecond, random)}",
        NodeKind.Landmark => Pick(Landmarks, random),
        _ => "Uncharted Point",
    };

    public static string PlaceholderDescription(NodeKind kind) => kind switch
    {
        NodeKind.Origin => "A creaking orbital ring where homesteaders outfit their starwagons.",
        NodeKind.Destination => "The promised harbor at the far edge of the Oregon Reach.",
        NodeKind.Outpost => "A trading post bolted to a tumbling asteroid; fuel, food and a smith for hire.",
        NodeKind.Settlement => "A hardscrabble homestead whose folk eye strangers warily.",
        NodeKind.Derelict => "A dead hauler drifting in silence, its cargo bays still sealed.",
        NodeKind.Hazard => "A stretch of space that wants to kill you in creative ways.",
        NodeKind.Raiders => "Claim-jumpers lurk here, preying on slow wagons.",
        NodeKind.Anomaly => "Something here bends light, sound and reason.",
        NodeKind.Landmark => "A famous waypoint where travelers carve their names into the void.",
        _ => "Uncharted.",
    };

    public static string AllyName(Random random) => $"{Pick(AllyFirst, random)} {Pick(AllyLast, random)}";

    private static string Pick(string[] values, Random random) => values[random.Next(values.Length)];
}
