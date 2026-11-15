using System.Text.Json.Serialization;

namespace Oregon.Contracts.Model;

/// <summary>Captain attributes that level up over the voyage.</summary>
/// <remarks>Serialized as a string everywhere (Temporal history, HTTP, SignalR) so reordering members never corrupts saves.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<StatKind>))]
public enum StatKind
{
    /// <summary>Imagination and artistry; anomalies and morale boosts.</summary>
    Creativity,

    /// <summary>Ethical judgment; moral dilemmas and compassion.</summary>
    Morality,

    /// <summary>Grit under pressure; adversarial events and hardship.</summary>
    Resilience,

    /// <summary>Persuasion and negotiation.</summary>
    Diplomacy,

    /// <summary>Engineering and problem-solving.</summary>
    Ingenuity,

    /// <summary>Hand-eye coordination; skirmish click battles.</summary>
    Reflexes
}
