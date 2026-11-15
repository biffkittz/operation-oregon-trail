namespace Oregon.Contracts;

/// <summary>
/// Limits shared by API validation, the workflow's update validators and the frontend.
/// </summary>
public static class GameLimits
{
    /// <summary>Maximum number of save slots, which is also the cap on concurrently running game workflows.</summary>
    public const int MaxSaveSlots = 10;

    /// <summary>Maximum characters accepted for a free-text challenge response.</summary>
    public const int MaxResponseLength = 600;

    /// <summary>Maximum characters accepted for a story seed.</summary>
    public const int MaxStorySeedLength = 1200;

    /// <summary>Maximum characters for captain and ship names.</summary>
    public const int MaxNameLength = 40;

    /// <summary>Number of journal entries embedded in each <see cref="Model.GameView"/>.</summary>
    public const int RecentJournalSize = 40;
}
