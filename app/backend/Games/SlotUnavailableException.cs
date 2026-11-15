namespace Oregon.Backend.Games;

/// <summary>The requested save slot is taken, or all slots are full (HTTP 409).</summary>
public sealed class SlotUnavailableException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="SlotUnavailableException"/> class.</summary>
    public SlotUnavailableException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SlotUnavailableException"/> class.</summary>
    /// <param name="message">Player-facing message.</param>
    public SlotUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SlotUnavailableException"/> class.</summary>
    /// <param name="message">Player-facing message.</param>
    /// <param name="innerException">Cause.</param>
    public SlotUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
