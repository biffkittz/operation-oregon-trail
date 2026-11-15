using Oregon.Contracts.Model;
using Oregon.Contracts.Workflows;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game.Rules;

/// <summary>Creates the initial state for a new voyage.</summary>
internal static class GameSetup
{
    public const int StartingLevel = 2;

    public static GameState CreateNew(GameWorkflowInput input, DateTime nowUtc)
    {
        var profile = DifficultyProfile.For(input.Settings.Difficulty);
        return new GameState
        {
            GameId = input.GameId,
            SlotNumber = input.SlotNumber,
            Settings = input.Settings,
            ProjectionTaskQueue = input.ProjectionTaskQueue,
            Phase = GamePhase.Initializing,
            Resources = new ResourcePool
            {
                Fuel = profile.StartFuel,
                Rations = profile.StartRations,
                Scrip = profile.StartScrip,
                Crew = profile.StartCrew,
                Hull = 100,
                Morale = 70,
            },
            Stats = [.. Enum.GetValues<StatKind>().Select(s => new StatProgress { Stat = s, Level = StartingLevel })],
            Version = 1,
            UpdatedAtUtc = nowUtc,
        };
    }

    /// <summary>Two distinct attributes start one level higher so every captain feels a little different.</summary>
    public static IReadOnlyList<StatKind> RollStrengths(GameState state, Random random)
    {
        List<StatKind> pool = [.. Enum.GetValues<StatKind>()];
        List<StatKind> picked = new(2);
        for (int i = 0; i < 2; i++)
        {
            int index = random.Next(pool.Count);
            picked.Add(pool[index]);
            pool.RemoveAt(index);
        }

        foreach (StatKind stat in picked)
        {
            ProgressionRules.Get(state, stat).Level = StartingLevel + 1;
        }

        return picked;
    }
}
