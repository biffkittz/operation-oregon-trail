using Oregon.Contracts;
using Oregon.Contracts.Model;
using Oregon.Contracts.Workflows;

namespace Oregon.Backend.Tests.Support;

/// <summary>Builds minimal valid <see cref="GameView"/> instances.</summary>
public static class TestViews
{
    public static GameView For(GameWorkflowInput input, long version = 1, GameStatus status = GameStatus.Active) => new()
    {
        GameId = input.GameId,
        SlotNumber = input.SlotNumber,
        Version = version,
        Status = status,
        Phase = status == GameStatus.Active ? GamePhase.ChoosingRoute : GamePhase.Finished,
        Title = input.Settings.Title,
        CaptainName = input.Settings.CaptainName,
        ShipName = input.Settings.ShipName,
        Tone = input.Settings.Tone,
        Difficulty = input.Settings.Difficulty,
        StorySeed = input.Settings.StorySeed,
        Day = (int)version,
        Resources = new ResourcesView(50, 50, 90, 70, 80, 5),
        Stats = [],
        UpgradePoints = 0,
        Allies = [],
        Map = new MapView([new MapNodeView("c0r0", "Independence Ring", NodeKind.Origin, 0, 0, 1, "Start", true, false)], [], "c0r0", "c7r0", 8),
        Journal = [],
        Prompt = new PromptView(),
        RepairJobs = [],
        Finale = status == GameStatus.Active ? null : new FinaleView(status, 1234, (int)version, "The end."),
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    public static GameWorkflowInput Input(Guid gameId, int slot) =>
        new(gameId, slot, new NewGameSettings("T", "C", "S", GameTone.Gritty, Difficulty.Normal, "A long enough seed."), TaskQueues.Web);
}
