using System.Text.Json.Serialization;

namespace Oregon.Backend.Data;

/// <summary>Typewriter speed for narrative text.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TextSpeed>))]
public enum TextSpeed
{
    /// <summary>No typewriter effect.</summary>
    Instant,

    /// <summary>Quick reveal.</summary>
    Fast,

    /// <summary>Classic terminal pace.</summary>
    Normal,

    /// <summary>Dramatic.</summary>
    Slow,
}
