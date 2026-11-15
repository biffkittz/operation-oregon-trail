using System.Text.Json.Serialization;

namespace Oregon.Contracts.Model;

/// <summary>Five-tier judgment applied to challenges, dilemmas and skirmishes.</summary>
/// <remarks>Serialized as a string everywhere (Temporal history, HTTP, SignalR) so reordering members never corrupts saves.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<Verdict>))]
public enum Verdict
{
    /// <summary>An outstanding result.</summary>
    CriticalSuccess,

    /// <summary>A clear success.</summary>
    Success,

    /// <summary>Mixed results.</summary>
    Partial,

    /// <summary>A clear failure.</summary>
    Failure,

    /// <summary>A disastrous result.</summary>
    CriticalFailure
}
