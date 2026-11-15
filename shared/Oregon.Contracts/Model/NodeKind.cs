using System.Text.Json.Serialization;

namespace Oregon.Contracts.Model;

/// <summary>What waits at a map node.</summary>
/// <remarks>Serialized as a string everywhere (Temporal history, HTTP, SignalR) so reordering members never corrupts saves.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<NodeKind>))]
public enum NodeKind
{
    /// <summary>The starting station.</summary>
    Origin,

    /// <summary>Trading post: buy supplies, commission repairs, haggle.</summary>
    Outpost,

    /// <summary>Homestead or camp: persuasion and moral challenges, possible allies.</summary>
    Settlement,

    /// <summary>Abandoned wreck: problem-solving challenges and salvage.</summary>
    Derelict,

    /// <summary>Natural hazard: a three-way dilemma.</summary>
    Hazard,

    /// <summary>Claim-jumper territory: a click-battle skirmish.</summary>
    Raiders,

    /// <summary>Strange phenomenon: creative or moral challenges.</summary>
    Anomaly,

    /// <summary>Famous waypoint: a moment of rest and wonder.</summary>
    Landmark,

    /// <summary>Willamette Landing, the end of the trail.</summary>
    Destination
}
