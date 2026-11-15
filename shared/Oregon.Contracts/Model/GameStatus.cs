using System.Text.Json.Serialization;

namespace Oregon.Contracts.Model;

/// <summary>Overall outcome of a save.</summary>
/// <remarks>Serialized as a string everywhere (Temporal history, HTTP, SignalR) so reordering members never corrupts saves.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<GameStatus>))]
public enum GameStatus
{
    /// <summary>Still being played.</summary>
    Active,

    /// <summary>Reached Willamette Landing.</summary>
    Victory,

    /// <summary>The voyage failed.</summary>
    Defeat
}
