using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game.Rules;

/// <summary>
/// Remembers key decisions as short facts. They are fed into every LLM prompt, which is how earlier choices
/// visibly branch later narration.
/// </summary>
internal static class StoryFlags
{
    public const int MaxFlags = 12;

    public static void Record(GameState state, string fact)
    {
        state.StoryFlags.Add($"Day {state.Day}: {fact}");
        if (state.StoryFlags.Count > MaxFlags)
        {
            state.StoryFlags.RemoveRange(0, state.StoryFlags.Count - MaxFlags);
        }
    }
}
