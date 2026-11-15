namespace Oregon.Worker.Game;

/// <summary>
/// A player command that is illegal in the current state. Thrown by validators so Temporal rejects the update
/// before it is written to history; the message is shown to the player verbatim.
/// </summary>
public sealed class GameRuleException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="GameRuleException"/> class.</summary>
    public GameRuleException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GameRuleException"/> class.</summary>
    /// <param name="message">Player-facing explanation.</param>
    public GameRuleException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="GameRuleException"/> class.</summary>
    /// <param name="message">Player-facing explanation.</param>
    /// <param name="innerException">Underlying cause.</param>
    public GameRuleException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
