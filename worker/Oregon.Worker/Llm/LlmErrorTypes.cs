namespace Oregon.Worker.Llm;

/// <summary>Temporal application error types raised by LLM activities.</summary>
public static class LlmErrorTypes
{
    /// <summary>Retrying cannot help (bad key, unknown model, safety block). Listed as non-retryable.</summary>
    public const string Permanent = "LlmPermanent";

    /// <summary>Rate limit, timeout or server error.</summary>
    public const string Transient = "LlmTransient";

    /// <summary>The model returned JSON that did not match the schema; a fresh sample usually fixes it.</summary>
    public const string MalformedOutput = "LlmMalformedOutput";
}
