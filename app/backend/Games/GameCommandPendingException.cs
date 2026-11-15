namespace Oregon.Backend.Games;

/// <summary>The command was accepted but did not finish within the HTTP timeout; the result arrives over SignalR (HTTP 202).</summary>
public sealed class GameCommandPendingException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="GameCommandPendingException"/> class.</summary>
    public GameCommandPendingException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GameCommandPendingException"/> class.</summary>
    /// <param name="message">Player-facing message.</param>
    public GameCommandPendingException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GameCommandPendingException"/> class.</summary>
    /// <param name="message">Player-facing message.</param>
    /// <param name="innerException">Cause.</param>
    public GameCommandPendingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
