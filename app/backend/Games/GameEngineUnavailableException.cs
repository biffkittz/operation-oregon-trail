namespace Oregon.Backend.Games;

/// <summary>Temporal could not be reached (HTTP 503).</summary>
public sealed class GameEngineUnavailableException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="GameEngineUnavailableException"/> class.</summary>
    public GameEngineUnavailableException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GameEngineUnavailableException"/> class.</summary>
    /// <param name="message">Player-facing message.</param>
    public GameEngineUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GameEngineUnavailableException"/> class.</summary>
    /// <param name="message">Player-facing message.</param>
    /// <param name="innerException">Cause.</param>
    public GameEngineUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
