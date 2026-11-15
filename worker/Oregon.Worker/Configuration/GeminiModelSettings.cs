using System.ComponentModel.DataAnnotations;

namespace Oregon.Worker.Configuration;

/// <summary>Model selection for one LLM purpose (judge or narrator).</summary>
public sealed class GeminiModelSettings
{
    /// <summary>Gets or sets the Gemini model ID, e.g. <c>gemini-3.8-flash</c>.</summary>
    [Required]
    public string Model { get; set; } = "gemini-3.8-flash";

    /// <summary>
    /// Gets or sets the thinking level ("low", "medium", "high"); empty to omit the setting for models that
    /// do not support thinking.
    /// </summary>
    public string? ThinkingLevel { get; set; }
}
