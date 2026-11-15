namespace Oregon.Backend.Games;

/// <summary>The workflow refused the command, e.g. it is illegal in the current phase (HTTP 409).</summary>
public sealed class GameCommandRejectedException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="GameCommandRejectedException"/> class.</summary>
    public GameCommandRejectedException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GameCommandRejectedException"/> class.</summary>
    /// <param name="message">Player-facing message.</param>
    public GameCommandRejectedException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GameCommandRejectedException"/> class.</summary>
    /// <param name="message">Player-facing message.</param>
    /// <param name="innerException">Cause.</param>
    public GameCommandRejectedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
