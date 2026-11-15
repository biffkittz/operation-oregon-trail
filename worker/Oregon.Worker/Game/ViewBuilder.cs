using Oregon.Contracts;
using Oregon.Contracts.Model;
using Oregon.Worker.Game.Rules;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game;

/// <summary>Projects internal state into the public <see cref="GameView"/>. Pure; safe to call from queries.</summary>
internal static class ViewBuilder
{
    public static GameView Build(GameState state)
    {
        HashSet<string> reachable = state.Phase == GamePhase.ChoosingRoute
            ? [.. state.Map.EdgesFrom(state.Map.CurrentNodeId).Select(e => e.To)]
            : [];

        return new GameView
        {
            GameId = state.GameId,
            SlotNumber = state.SlotNumber,
            Version = state.Version,
            Status = state.Status,
            Phase = state.Phase,
            Title = state.Settings.Title,
            CaptainName = state.Settings.CaptainName,
            ShipName = state.Settings.ShipName,
            Tone = state.Settings.Tone,
            Difficulty = state.Settings.Difficulty,
            StorySeed = state.Settings.StorySeed,
            Day = state.Day,
            Resources = state.Resources.ToView(),
            Stats = [.. state.Stats.Select(s => new StatView(s.Stat, s.Level, s.Xp, ProgressionRules.XpToNext(s.Level)))],
            UpgradePoints = state.UpgradePoints,
            Allies = [.. state.Allies.Select(AllyRules.ToView)],
            Map = new MapView(
                [.. state.Map.Nodes.Select(n => new MapNodeView(
                    n.Id,
                    n.Name,
                    n.Kind,
                    n.Column,
                    n.Row,
                    n.RowsInColumn,
                    n.Description,
                    state.Map.VisitedNodeIds.Contains(n.Id),
                    reachable.Contains(n.Id)))],
                [.. state.Map.Edges.Select(e => new MapEdgeView(e.From, e.To, e.Days))],
                state.Map.CurrentNodeId,
                state.Map.DestinationNodeId,
                state.Map.Columns),
            Journal = [.. state.RecentJournal.TakeLast(GameLimits.RecentJournalSize)],
            Prompt = BuildPrompt(state),
            RepairJobs = [.. state.RepairJobs.Select(j => new RepairJobView(j.Id, j.Label, j.HullAmount, Utc(j.ReadyAtUtc)))],
            Finale = state.Finale,
            UpdatedAt = Utc(state.UpdatedAtUtc),
        };
    }

    private static PromptView BuildPrompt(GameState state) => state.Phase switch
    {
        GamePhase.Narrative when state.Narrative is { } n => new PromptView(
            Narrative: new NarrativePromptView(n.Headline, n.Text, n.Verdict, [.. n.Changes], n.ContinueLabel)),
        GamePhase.ChoosingRoute => new PromptView(
            Routes: TravelRules.Routes(state),
            Outpost: state.Outpost is { } outpost
                ? OutpostRules.ToView(outpost) with { Name = state.Map.FindNode(outpost.NodeId)?.Name ?? "Outpost" }
                : null,
            EmergencyFuel: EmergencyFuelRules.Offer(state)),
        GamePhase.AwaitingResponse when state.Challenge is { } c => new PromptView(
            Challenge: new ChallengePromptView(
                c.Kind,
                c.Stat,
                c.Title,
                c.Description,
                c.NpcName,
                c.NpcLine,
                $"Success: {c.StakesOnSuccess}. Failure: {c.StakesOnFailure}.",
                c.DeadlineUtc is { } deadline ? Utc(deadline) : null,
                GameLimits.MaxResponseLength)),
        GamePhase.AwaitingChoice when state.Dilemma is { } d => new PromptView(
            Dilemma: new DilemmaPromptView(
                d.Title,
                d.Description,
                [.. d.Options.Select(o => new DilemmaOptionView(o.Id, o.Label, o.Hint))],
                d.DeadlineUtc is { } deadline ? Utc(deadline) : null)),
        GamePhase.Skirmish when state.Skirmish is { } s => new PromptView(
            Skirmish: new SkirmishPromptView(s.Title, s.Description, s.Spec)),
        _ => new PromptView(),
    };

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);
}
