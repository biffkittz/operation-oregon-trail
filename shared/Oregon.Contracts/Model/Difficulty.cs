using System.Text.Json.Serialization;

namespace Oregon.Contracts.Model;

/// <summary>Difficulty profile; scales resources, hazards and time limits.</summary>
/// <remarks>Serialized as a string everywhere (Temporal history, HTTP, SignalR) so reordering members never corrupts saves.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<Difficulty>))]
public enum Difficulty
{
    /// <summary>Generous supplies, long time limits.</summary>
    Easy,

    /// <summary>The intended experience.</summary>
    Normal,

    /// <summary>Lean supplies, frequent hazards, short time limits.</summary>
    Hard
}
