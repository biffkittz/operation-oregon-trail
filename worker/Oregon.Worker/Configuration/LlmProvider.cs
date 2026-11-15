namespace Oregon.Worker.Configuration;

/// <summary>Which LLM implementation the activities use.</summary>
public enum LlmProvider
{
    /// <summary>Gemini when an API key is configured, otherwise the offline fake (local development default).</summary>
    Auto,

    /// <summary>Google Gemini; startup fails without an API key.</summary>
    Gemini,

    /// <summary>Deterministic offline fake for tests and keyless development.</summary>
    Fake,
}
