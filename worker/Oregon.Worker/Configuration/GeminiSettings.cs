using System.ComponentModel.DataAnnotations;

namespace Oregon.Worker.Configuration;

/// <summary>Google Gemini API settings.</summary>
/// <remarks>
/// Two model profiles exist because judging needs reasoning quality while narration needs speed: measured on
/// 2026-11-14, gemini-3.8-flash (low thinking) answered a judgment in ~3 s and gemini-3.5-flash-lite narrated in ~1 s.
/// </remarks>
public sealed class GeminiSettings
{
    /// <summary>Gets or sets the REST base URL.</summary>
    [Required]
    public Uri BaseUrl { get; set; } = new("https://generativelanguage.googleapis.com/");

    /// <summary>Gets or sets the API key (bound from the <c>Llm__Gemini__ApiKey</c> secret in Azure).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Gets or sets the model used to judge player responses.</summary>
    [Required]
    public GeminiModelSettings Judge { get; set; } = new();

    /// <summary>Gets or sets the model used to write narration, encounters and world flavor.</summary>
    [Required]
    public GeminiModelSettings Narrator { get; set; } = new() { Model = "gemini-3.5-flash-lite" };
}
