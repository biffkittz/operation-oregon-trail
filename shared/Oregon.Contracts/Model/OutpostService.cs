using System.Text.Json.Serialization;

namespace Oregon.Contracts.Model;

/// <summary>Services offered at outposts.</summary>
/// <remarks>Serialized as a string everywhere (Temporal history, HTTP, SignalR) so reordering members never corrupts saves.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<OutpostService>))]
public enum OutpostService
{
    /// <summary>Buy fuel in units of ten.</summary>
    BuyFuel,

    /// <summary>Buy rations in units of ten.</summary>
    BuyRations,

    /// <summary>Commission a hull repair that completes on a real-time timer.</summary>
    CommissionRepair,

    /// <summary>Try to talk the smith down on the repair price (starts a persuasion challenge).</summary>
    Haggle
}
