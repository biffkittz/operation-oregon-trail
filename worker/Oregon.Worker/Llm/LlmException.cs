namespace Oregon.Worker.Llm;

/// <summary>An LLM provider failure, classified so Temporal knows whether retrying can help.</summary>
public sealed class LlmException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="LlmException"/> class.</summary>
    public LlmException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LlmException"/> class.</summary>
    /// <param name="message">Description.</param>
    public LlmException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LlmException"/> class.</summary>
    /// <param name="message">Description.</param>
    /// <param name="innerException">Cause.</param>
    public LlmException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LlmException"/> class.</summary>
    /// <param name="message">Description.</param>
    /// <param name="isTransient">Whether a retry may succeed (rate limits, 5xx, malformed output).</param>
    /// <param name="retryAfter">Provider-suggested delay before retrying.</param>
    public LlmException(string message, bool isTransient, TimeSpan? retryAfter = null)
        : base(message)
    {
        IsTransient = isTransient;
        RetryAfter = retryAfter;
    }

    /// <summary>Gets a value indicating whether retrying may succeed.</summary>
    public bool IsTransient { get; }

    /// <summary>Gets the provider-suggested retry delay, if any.</summary>
    public TimeSpan? RetryAfter { get; }
}
