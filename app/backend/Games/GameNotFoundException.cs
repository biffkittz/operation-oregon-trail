namespace Oregon.Backend.Games;

/// <summary>The save or its workflow does not exist (HTTP 404).</summary>
public sealed class GameNotFoundException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="GameNotFoundException"/> class.</summary>
    public GameNotFoundException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GameNotFoundException"/> class.</summary>
    /// <param name="message">Player-facing message.</param>
    public GameNotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GameNotFoundException"/> class.</summary>
    /// <param name="message">Player-facing message.</param>
    /// <param name="innerException">Cause.</param>
    public GameNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
