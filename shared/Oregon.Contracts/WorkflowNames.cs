namespace Oregon.Contracts;

/// <summary>
/// Stable Temporal workflow type names. Renaming a class must never change these strings, otherwise
/// running saves would no longer match a registered workflow type.
/// </summary>
public static class WorkflowNames
{
    /// <summary>Long-running workflow that owns one save slot's game state.</summary>
    public const string Game = "OregonGame";

    /// <summary>Short-lived workflow that asks the LLM for a story seed.</summary>
    public const string StorySeed = "OregonStorySeed";
}
