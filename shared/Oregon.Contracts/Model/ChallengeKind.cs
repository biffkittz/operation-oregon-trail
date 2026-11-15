using System.Text.Json.Serialization;

namespace Oregon.Contracts.Model;

/// <summary>Flavor of a free-text challenge; determines the tested stat and the stakes.</summary>
/// <remarks>Serialized as a string everywhere (Temporal history, HTTP, SignalR) so reordering members never corrupts saves.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<ChallengeKind>))]
public enum ChallengeKind
{
    /// <summary>Convince someone to do something.</summary>
    Persuasion,

    /// <summary>Solve a technical problem.</summary>
    Ingenuity,

    /// <summary>Improvise something imaginative.</summary>
    Creativity,

    /// <summary>Make a hard ethical call.</summary>
    Morality,

    /// <summary>React to a sudden adversarial event while travelling.</summary>
    Hardship,

    /// <summary>Talk an outpost merchant down on price.</summary>
    Haggle
}
