using System.ComponentModel.DataAnnotations;

namespace Oregon.Worker.Configuration;

/// <summary>LLM integration settings.</summary>
public sealed class LlmSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Llm";

    /// <summary>Gets or sets which implementation to use.</summary>
    public LlmProvider Provider { get; set; } = LlmProvider.Auto;

    /// <summary>Gets or sets the per-request HTTP timeout. Temporal's activity timeout is the outer bound.</summary>
    [Range(5, 300)]
    public int TimeoutSeconds { get; set; } = 45;

    /// <summary>Gets or sets the Gemini settings.</summary>
    [Required]
    public GeminiSettings Gemini { get; set; } = new();
}
