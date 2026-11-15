using System.Text.Json.Serialization;

namespace Oregon.Contracts.Model;

/// <summary>Narrative tone requested from the LLM narrator.</summary>
/// <remarks>Serialized as a string everywhere (Temporal history, HTTP, SignalR) so reordering members never corrupts saves.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<GameTone>))]
public enum GameTone
{
    /// <summary>Playful and absurd, warm humor.</summary>
    Whimsical,

    /// <summary>Earnest optimism and wonder.</summary>
    Hopeful,

    /// <summary>Dusty, hard-edged frontier realism with dark humor.</summary>
    Gritty,

    /// <summary>Somber, melancholy and bleak, but poetic.</summary>
    Morose
}
