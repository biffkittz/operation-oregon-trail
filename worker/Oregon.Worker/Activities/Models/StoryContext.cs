using Oregon.Contracts.Model;

namespace Oregon.Worker.Activities.Models;

/// <summary>
/// Compact summary of the voyage sent with every LLM request so narration stays consistent and choices
/// (story flags, allies, recent events) visibly branch the story.
/// </summary>
/// <param name="Title">Voyage title.</param>
/// <param name="StorySeed">The premise.</param>
/// <param name="Tone">Requested tone.</param>
/// <param name="Difficulty">Difficulty.</param>
/// <param name="CaptainName">Captain's name.</param>
/// <param name="ShipName">Ship's name.</param>
/// <param name="Day">In-game day.</param>
/// <param name="LocationName">Current node name.</param>
/// <param name="LocationDescription">Current node description.</param>
/// <param name="Resources">Supplies.</param>
/// <param name="Allies">Allies as "Name (Role)".</param>
/// <param name="StoryFlags">Story facts established by earlier choices.</param>
/// <param name="RecentEvents">Most recent journal lines, oldest first.</param>
public sealed record StoryContext(
    string Title,
    string StorySeed,
    GameTone Tone,
    Difficulty Difficulty,
    string CaptainName,
    string ShipName,
    int Day,
    string LocationName,
    string LocationDescription,
    ResourcesView Resources,
    IReadOnlyList<string> Allies,
    IReadOnlyList<string> StoryFlags,
    IReadOnlyList<string> RecentEvents);
