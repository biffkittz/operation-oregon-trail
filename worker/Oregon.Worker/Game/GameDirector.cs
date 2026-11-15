using System.Globalization;
using Oregon.Contracts.Model;
using Oregon.Worker.Activities.Models;
using Oregon.Worker.Game.Commands;
using Oregon.Worker.Game.Rules;
using Oregon.Worker.Game.State;

namespace Oregon.Worker.Game;

/// <summary>
/// Orchestrates the game loop: what happens when the player acts or a timer fires.
/// </summary>
/// <remarks>
/// <para>The director is deterministic as long as its collaborators are: the workflow passes Temporal's clock,
/// Temporal's replay-safe <see cref="Random"/> and an <see cref="IStoryteller"/> backed by activities. That lets
/// the same code run inside the workflow and in plain unit tests.</para>
/// <para>Division of labor: <c>Rules/*</c> decide consequences (pure functions); the storyteller (LLM) only
/// writes prose and judges free text; this class sequences the two and records the journal.</para>
/// </remarks>
internal sealed class GameDirector(GameState state, IStoryteller storyteller, IGameClock clock, Func<Random> random)
{
    /// <summary>Grace period after a deadline before a late answer is ruled out (absorbs network latency).</summary>
    public static readonly TimeSpan DeadlineGrace = TimeSpan.FromSeconds(3);

    private static readonly string[] OptionIds = ["A", "B", "C"];

    public GameState State => state;

    private DateTime Now => clock.UtcNow;

    private DifficultyProfile Profile => DifficultyProfile.For(state.Settings.Difficulty);

    /// <summary>Generates the map, asks the LLM to name it and shows the prologue.</summary>
    public async Task InitializeAsync()
    {
        IReadOnlyList<StatKind> strengths = GameSetup.RollStrengths(state, random());
        state.Map = MapGenerator.Generate(Profile, random());
        Touch();

        WorldGenerationRequest request = new(
            StoryContextFactory.Create(state),
            [.. state.Map.Nodes.Select(n => new WorldNodeSketch(n.Id, n.Kind, n.Column, n.Name))]);
        WorldFlavor flavor = await storyteller.DescribeWorldAsync(request);
        ApplyWorldFlavor(flavor);

        Journal.Write(state, JournalEntryKind.Narration, flavor.Prologue, Now);
        Journal.Write(state, JournalEntryKind.System, $"Your strengths: {strengths[0]} and {strengths[1]}.", Now);
        ShowNarrative("Prologue", flavor.Prologue, verdict: null, changes: [], NextStep.BeginVoyage, "Cast off");
        Touch();
    }

    /// <summary>Applies a validated player command.</summary>
    public async Task HandleAsync(PlayerCommand command)
    {
        CommandValidator.Validate(state, command, Now);
        state.TurnCount++;
        switch (command)
        {
            case ContinuePlayerCommand:
                await ContinueAsync();
                break;
            case RoutePlayerCommand route:
                await DepartAsync(route.Command.NodeId);
                break;
            case ResponsePlayerCommand response:
                await ResolveChallengeAsync(state.Challenge!, response.Command.Text.Trim());
                break;
            case OptionPlayerCommand option:
                await ResolveDilemmaAsync(option.Command.OptionId);
                break;
            case SkirmishPlayerCommand skirmish:
                await ResolveSkirmishAsync(skirmish.Command);
                break;
            case OutpostPlayerCommand outpost:
                await UseOutpostAsync(outpost.Command);
                break;
            case UpgradePlayerCommand upgrade:
                SpendUpgrade(upgrade.Command.Stat);
                break;
            default:
                throw new GameRuleException("Unknown command.");
        }

        await EndIfStrandedAsync();
        Touch();
    }

    /// <summary>Earliest moment a timer (deadline or repair job) needs attention, or null when none is pending.</summary>
    public DateTime? NextTimerDueUtc()
    {
        DateTime? due = OutpostRules.NextJobDueUtc(state);
        if (state.Phase == GamePhase.AwaitingResponse && state.Challenge?.DeadlineUtc is { } challengeDeadline)
        {
            due = Earliest(due, challengeDeadline + DeadlineGrace);
        }

        if (state.Phase == GamePhase.AwaitingChoice && state.Dilemma?.DeadlineUtc is { } dilemmaDeadline)
        {
            due = Earliest(due, dilemmaDeadline + DeadlineGrace);
        }

        return due;
    }

    /// <summary>Handles every timer that has come due: finished repairs and expired deadlines.</summary>
    public async Task HandleTimersAsync()
    {
        DateTime now = Now;
        List<ResourceChangeView> jobChanges = [];
        foreach (RepairJob job in OutpostRules.CompleteDueJobs(state, now, jobChanges))
        {
            Journal.Write(state, JournalEntryKind.Event, $"The smith sets down her tools: {job.Label.ToLowerInvariant()} complete.", now);
        }

        if (state.Phase == GamePhase.AwaitingResponse && state.Challenge is { DeadlineUtc: { } challengeDeadline } challenge
            && challengeDeadline + DeadlineGrace <= now)
        {
            await ResolveChallengeAsync(challenge, response: null);
        }
        else if (state.Phase == GamePhase.AwaitingChoice && state.Dilemma is { DeadlineUtc: { } dilemmaDeadline } dilemma
            && dilemmaDeadline + DeadlineGrace <= now)
        {
            await ResolveDilemmaTimeoutAsync(dilemma);
        }

        await EndIfStrandedAsync();
        Touch();
    }

    private async Task ContinueAsync()
    {
        PendingNarrative narrative = state.Narrative!;
        state.Narrative = null;
        switch (narrative.Next)
        {
            case NextStep.ArriveAtPendingNode when narrative.PendingNodeId is not null:
                await ArriveAsync(narrative.PendingNodeId);
                break;
            case NextStep.Finale:
                await FinishAsync(GameStatus.Defeat, narrative.DefeatReason ?? "The voyage has ended.");
                break;
            default:
                await EnterRouteChoiceAsync();
                break;
        }
    }

    private async Task EnterRouteChoiceAsync()
    {
        state.Phase = GamePhase.ChoosingRoute;
        await EndIfStrandedAsync();
    }

    /// <summary>
    /// Ends the voyage when the ship can neither afford any route nor buy fuel here. Checked after every command
    /// and timer (not just when docking): spending the last scrip on rations can strand a ship mid-visit, and
    /// without this check the player would be stuck with no legal move.
    /// </summary>
    private async Task EndIfStrandedAsync()
    {
        if (state.Status == GameStatus.Active && state.Phase == GamePhase.ChoosingRoute && EndgameRules.IsStranded(state))
        {
            await FinishAsync(GameStatus.Defeat, "Out of fuel with no way to buy more, the wagon drifts to a stop.");
        }
    }

    private async Task DepartAsync(string nodeId)
    {
        MapEdge edge = TravelRules.FindRoute(state, nodeId)!;
        MapNode target = state.Map.FindNode(nodeId)!;
        if (state.RepairJobs.Count > 0)
        {
            state.RepairJobs.Clear();
            Journal.Write(state, JournalEntryKind.Event, "You cast off before the smith finished. The half-done patch rattles loose.", Now);
        }

        state.Outpost = null;
        List<ResourceChangeView> changes = [];
        bool starving = TravelRules.Travel(state, edge, random(), changes);
        string hunger = starving ? " The pantry is empty and the crew goes hungry." : string.Empty;
        Journal.Write(
            state,
            JournalEntryKind.Event,
            $"The {state.Settings.ShipName} burns {Plural(edge.Days, "day")} toward {target.Name}.{hunger}",
            Now);

        if (EndgameRules.DefeatReason(state) is { } defeat)
        {
            ShowNarrative("Lost in the Black", $"Somewhere short of {target.Name}, the voyage falters. {defeat}", Verdict.CriticalFailure, changes, NextStep.Finale, "Face the end", defeatReason: defeat);
            return;
        }

        if (TravelRules.RollTravelEvent(Profile, random()))
        {
            await PresentChallengeAsync(EncounterPlanner.Hardship(state, random()), ChallengeOrigin.TravelEvent, nodeId);
            return;
        }

        await ArriveAsync(nodeId);
    }

    private async Task ArriveAsync(string nodeId)
    {
        MapNode node = state.Map.FindNode(nodeId)!;
        state.Map.CurrentNodeId = nodeId;
        if (!state.Map.VisitedNodeIds.Contains(nodeId))
        {
            state.Map.VisitedNodeIds.Add(nodeId);
        }

        state.LegsCompleted++;
        List<ResourceChangeView> changes = [];
        AllyRules.ApplyLegPerks(state, changes);
        Journal.Write(state, JournalEntryKind.Event, $"Day {state.Day}: docked at {node.Name}.", Now);

        switch (node.Kind)
        {
            case NodeKind.Destination:
                await FinishAsync(GameStatus.Victory, defeatReason: null);
                break;

            case NodeKind.Outpost:
                state.Outpost = OutpostRules.Enter(state, node);
                NarrationResult arrival = await storyteller.NarrateAsync(new NarrationRequest(
                    StoryContextFactory.Create(state),
                    "arrival at a frontier trading outpost",
                    $"The wagon docks at {node.Name}. {node.Description} Fuel, rations and a smith for hire are available here.",
                    3));
                Journal.Write(state, JournalEntryKind.Narration, arrival.Text, Now);
                ShowNarrative(node.Name, arrival.Text, verdict: null, changes, NextStep.ReturnToRoute, "Browse the outpost");
                break;

            case NodeKind.Landmark:
                ResourceRules.Apply(state, new ResourceDelta(Morale: 6), changes);
                ProgressionRules.AddXp(state, StatKind.Creativity, 10, changes);
                NarrationResult wonder = await storyteller.NarrateAsync(new NarrationRequest(
                    StoryContextFactory.Create(state),
                    "a moment of rest and wonder at a famous landmark",
                    $"The crew rests at {node.Name}. {node.Description} Spirits lift.",
                    3));
                Journal.Write(state, JournalEntryKind.Narration, wonder.Text, Now);
                ShowNarrative(node.Name, wonder.Text, verdict: null, changes, NextStep.ReturnToRoute, "Carve your name and move on");
                break;

            case NodeKind.Hazard:
                await PresentDilemmaAsync(node);
                break;

            case NodeKind.Raiders:
                await PresentSkirmishAsync(node);
                break;

            default:
                await PresentChallengeAsync(EncounterPlanner.ForNode(state, node, random()), ChallengeOrigin.NodeEncounter, pendingNodeId: null);
                break;
        }
    }

    private async Task PresentChallengeAsync(ChallengePlan plan, ChallengeOrigin origin, string? pendingNodeId)
    {
        DifficultyProfile profile = Profile;
        (string onSuccess, string onFailure) = plan.Kind == ChallengeKind.Haggle
            ? ("a steep discount on the repair", "full price, or worse if you insult her")
            : (StakesText.Describe(plan.Reward.Scale(profile.RewardMultiplier), plan.RewardUpgradePoints, plan.AllyOffer),
               StakesText.Describe(plan.Penalty.Scale(profile.PenaltyMultiplier)));

        ChallengeContent content = await storyteller.CreateChallengeAsync(new ChallengeRequest(
            StoryContextFactory.Create(state), plan.Kind, plan.Stat, plan.Situation, onSuccess, onFailure));

        // The character the captain has to win over is the one who joins on success.
        if (plan.AllyOffer is not null && !string.IsNullOrWhiteSpace(content.NpcName))
        {
            plan.AllyOffer.Name = content.NpcName.Trim();
        }

        state.Challenge = new ActiveChallenge
        {
            Kind = plan.Kind,
            Stat = plan.Stat,
            Origin = origin,
            Title = content.Title,
            Description = content.Description,
            NpcName = string.IsNullOrWhiteSpace(content.NpcName) ? null : content.NpcName,
            NpcLine = string.IsNullOrWhiteSpace(content.NpcLine) ? null : content.NpcLine,
            StakesOnSuccess = onSuccess,
            StakesOnFailure = onFailure,
            Reward = plan.Reward,
            Penalty = plan.Penalty,
            RewardUpgradePoints = plan.RewardUpgradePoints,
            AllyOffer = plan.AllyOffer,
            PendingNodeId = pendingNodeId,

            // The clock starts after the LLM finishes writing the prompt, so its latency never eats the player's time.
            DeadlineUtc = plan.Seconds is { } seconds ? Now.AddSeconds(seconds) : null,
        };
        state.Phase = GamePhase.AwaitingResponse;
        Journal.Write(state, JournalEntryKind.Event, $"{content.Title}: {content.Description}", Now);
        if (state.Challenge.NpcName is not null && state.Challenge.NpcLine is not null)
        {
            Journal.Write(state, JournalEntryKind.Npc, $"{state.Challenge.NpcName}: \"{state.Challenge.NpcLine}\"", Now);
        }
    }

    private async Task ResolveChallengeAsync(ActiveChallenge challenge, string? response)
    {
        Resolution resolution;
        string narrative;
        string npcReply = string.Empty;

        if (response is null)
        {
            resolution = ChallengeRules.ResolveTimeout(state, challenge);
            narrative = challenge.Kind == ChallengeKind.Hardship
                ? "You froze. Seconds stretched into a minute, and the trouble did not wait for you."
                : $"{challenge.NpcName ?? "The moment"} will not wait forever. Your silence answers for you.";
        }
        else
        {
            Journal.Write(state, JournalEntryKind.Player, response, Now);
            EvaluationResult judgment = await storyteller.JudgeAsync(new EvaluationRequest(
                StoryContextFactory.Create(state),
                challenge.Kind,
                challenge.Title,
                challenge.Description,
                challenge.NpcName,
                challenge.StakesOnSuccess,
                challenge.StakesOnFailure,
                challenge.Stat,
                ProgressionRules.EffectiveLevel(state, challenge.Stat),
                response));

            // Belt and braces: whatever verdict the model returned, a detected manipulation attempt is a disaster.
            Verdict verdict = judgment.ManipulationDetected ? Verdict.CriticalFailure : judgment.Verdict;
            resolution = ChallengeRules.Resolve(state, challenge, verdict);
            narrative = judgment.Narrative;
            npcReply = judgment.NpcReply;
        }

        Journal.Write(state, JournalEntryKind.Outcome, narrative, Now);
        string text = narrative;
        if (!string.IsNullOrWhiteSpace(npcReply) && challenge.NpcName is not null)
        {
            Journal.Write(state, JournalEntryKind.Npc, $"{challenge.NpcName}: \"{npcReply}\"", Now);
            text = $"{narrative}\n\n{challenge.NpcName}: \"{npcReply}\"";
        }

        if (resolution.AllyJoined is { } ally)
        {
            Journal.Write(state, JournalEntryKind.Event, $"{ally.Name} joins the crew as your {ally.Role.ToLowerInvariant()}.", Now);
        }

        state.Challenge = null;
        NextStep next = challenge.Origin == ChallengeOrigin.TravelEvent ? NextStep.ArriveAtPendingNode : NextStep.ReturnToRoute;
        ShowOutcome(VerdictText.Headline(resolution.Verdict), text, resolution, next, challenge.PendingNodeId);
    }

    private async Task PresentDilemmaAsync(MapNode node)
    {
        (string situation, IReadOnlyList<DilemmaArchetype> archetypes) = EncounterPlanner.Dilemma(random());
        List<DilemmaOptionBrief> briefs = [.. archetypes.Select((a, i) => new DilemmaOptionBrief(OptionIds[i], a.ToString(), DilemmaRules.Guidance(a)))];
        DilemmaContent content = await storyteller.CreateDilemmaAsync(new DilemmaRequest(
            StoryContextFactory.Create(state), $"At {node.Name}, {situation}", briefs));

        state.Dilemma = new ActiveDilemma
        {
            Title = content.Title,
            Description = content.Description,
            Options = [.. briefs.Select((brief, i) =>
            {
                DilemmaOptionContent? written = content.Options.FirstOrDefault(o => string.Equals(o.Id, brief.Id, StringComparison.Ordinal));
                return new DilemmaOption
                {
                    Id = brief.Id,
                    Archetype = archetypes[i],
                    Label = string.IsNullOrWhiteSpace(written?.Label) ? DilemmaRules.DefaultLabel(archetypes[i]) : written.Label,
                    Hint = string.IsNullOrWhiteSpace(written?.Hint) ? DilemmaRules.DefaultHint(archetypes[i]) : written.Hint,
                };
            })],
            DeadlineUtc = Profile.DilemmaSeconds is { } seconds ? Now.AddSeconds(seconds) : null,
        };
        state.Phase = GamePhase.AwaitingChoice;
        Journal.Write(state, JournalEntryKind.Event, $"{content.Title}: {content.Description}", Now);
    }

    private async Task ResolveDilemmaAsync(string optionId)
    {
        ActiveDilemma dilemma = state.Dilemma!;
        DilemmaOption option = dilemma.Options.First(o => string.Equals(o.Id, optionId, StringComparison.Ordinal));
        Journal.Write(state, JournalEntryKind.Player, $"You decide: {option.Label}.", Now);

        Resolution resolution = DilemmaRules.Resolve(state, dilemma, option, random());
        string allyNote = resolution.AllyJoined is { } ally ? $" {ally.Name} is so grateful they join the crew." : string.Empty;
        NarrationResult narration = await storyteller.NarrateAsync(new NarrationRequest(
            StoryContextFactory.Create(state),
            "the outcome of the captain's choice in a hazard",
            $"Hazard: {dilemma.Title}. {dilemma.Description} The captain chose: {option.Label}. " +
            $"Result: {VerdictText.Describe(resolution.Verdict)}. Consequences: {DescribeChanges(resolution.Changes)}.{allyNote}",
            3));

        Journal.Write(state, JournalEntryKind.Outcome, narration.Text, Now);
        state.Dilemma = null;
        ShowOutcome(narration.Headline, narration.Text, resolution, NextStep.ReturnToRoute, pendingNodeId: null);
    }

    private async Task ResolveDilemmaTimeoutAsync(ActiveDilemma dilemma)
    {
        Resolution resolution = DilemmaRules.ResolveTimeout(state, dilemma);
        NarrationResult narration = await storyteller.NarrateAsync(new NarrationRequest(
            StoryContextFactory.Create(state),
            "the hazard overtakes a captain who hesitated too long",
            $"Hazard: {dilemma.Title}. {dilemma.Description} The captain hesitated and the hazard decided. Consequences: {DescribeChanges(resolution.Changes)}.",
            3));
        Journal.Write(state, JournalEntryKind.Outcome, narration.Text, Now);
        state.Dilemma = null;
        ShowOutcome("Too Slow", narration.Text, resolution, NextStep.ReturnToRoute, pendingNodeId: null);
    }

    private async Task PresentSkirmishAsync(MapNode node)
    {
        SkirmishSpecView spec = EncounterPlanner.Skirmish(state, random());
        NarrationResult intro = await storyteller.NarrateAsync(new NarrationRequest(
            StoryContextFactory.Create(state),
            "raiders attack and a skirmish is about to begin",
            $"Claim-jumpers out of {node.Name} swarm the wagon with {spec.Targets} armed drones. The captain must man the scatter cannon.",
            3));
        state.Skirmish = new ActiveSkirmish { Title = intro.Headline, Description = intro.Text, Spec = spec };
        state.Phase = GamePhase.Skirmish;
        Journal.Write(state, JournalEntryKind.Event, $"{intro.Headline}: {intro.Text}", Now);
    }

    private async Task ResolveSkirmishAsync(SkirmishResultCommand result)
    {
        ActiveSkirmish skirmish = state.Skirmish!;
        Resolution resolution = SkirmishRules.Resolve(state, skirmish, result);
        Journal.Write(
            state,
            JournalEntryKind.Player,
            $"You fire {Plural(result.ShotsFired, "shot")} and down {result.Hits} of {skirmish.Spec.Targets} drones.",
            Now);
        NarrationResult narration = await storyteller.NarrateAsync(new NarrationRequest(
            StoryContextFactory.Create(state),
            "the aftermath of a skirmish with raiders",
            $"{resolution.Summary}. Consequences: {DescribeChanges(resolution.Changes)}.",
            3));
        Journal.Write(state, JournalEntryKind.Outcome, narration.Text, Now);
        state.Skirmish = null;
        ShowOutcome(narration.Headline, narration.Text, resolution, NextStep.ReturnToRoute, pendingNodeId: null);
    }

    private async Task UseOutpostAsync(OutpostCommand command)
    {
        List<ResourceChangeView> changes = [];
        switch (command.Service)
        {
            case OutpostService.BuyFuel or OutpostService.BuyRations:
                OutpostRules.Buy(state, command, changes);
                string item = command.Service == OutpostService.BuyFuel ? "fuel" : "rations";
                int spent = -changes.Where(c => c.Label == "Scrip").Sum(c => c.Delta);
                Journal.Write(state, JournalEntryKind.Event, $"Bought {OutpostRules.UnitSize * command.Quantity} {item} for {spent} scrip.", Now);
                break;

            case OutpostService.CommissionRepair:
                RepairJob job = OutpostRules.Commission(state, Now, changes);
                Journal.Write(
                    state,
                    JournalEntryKind.Event,
                    $"The smith fires up her torch: {job.Label.ToLowerInvariant()}, ready in {Plural(state.Outpost!.RepairSeconds, "second")}.",
                    Now);
                break;

            case OutpostService.Haggle:
                await PresentChallengeAsync(EncounterPlanner.Haggle(state), ChallengeOrigin.Haggle, pendingNodeId: null);
                break;

            default:
                throw new GameRuleException("Unknown outpost service.");
        }
    }

    private void SpendUpgrade(StatKind stat)
    {
        List<ResourceChangeView> changes = [];
        ProgressionRules.SpendUpgrade(state, stat, changes);
        Journal.Write(state, JournalEntryKind.System, $"Upgrade: {stat} rises to level {ProgressionRules.Level(state, stat)}.", Now);
    }

    private async Task FinishAsync(GameStatus status, string? defeatReason)
    {
        bool victory = status == GameStatus.Victory;
        int score = EndgameRules.Score(state, victory);
        string facts = victory
            ? $"After {Plural(state.Day, "day")}, the {state.Settings.ShipName} reaches Willamette Landing with {state.Resources.Crew} souls aboard " +
              $"and {Plural(state.Allies.Count, "ally", "allies")}. Final score {score}."
            : $"The voyage ends on day {state.Day}: {defeatReason} Final score {score}.";
        NarrationResult epilogue = await storyteller.NarrateAsync(new NarrationRequest(
            StoryContextFactory.Create(state),
            victory ? "victory epilogue: the wagon reaches Willamette Landing" : "defeat epilogue",
            facts,
            5));

        state.Finale = new FinaleView(status, score, state.Day, epilogue.Text);
        state.Status = status;
        state.Phase = GamePhase.Finished;
        state.Challenge = null;
        state.Dilemma = null;
        state.Skirmish = null;
        state.Narrative = null;
        state.Outpost = null;
        state.RepairJobs.Clear();
        Journal.Write(state, JournalEntryKind.Narration, epilogue.Text, Now);
        Journal.Write(state, JournalEntryKind.System, $"Final score: {score.ToString(CultureInfo.InvariantCulture)}.", Now);
    }

    private void ShowOutcome(string headline, string text, Resolution resolution, NextStep next, string? pendingNodeId)
    {
        if (EndgameRules.DefeatReason(state) is { } defeat)
        {
            ShowNarrative(headline, $"{text}\n\n{defeat}", resolution.Verdict, resolution.Changes, NextStep.Finale, "Face the end", defeatReason: defeat);
            return;
        }

        string label = next == NextStep.ArriveAtPendingNode ? "Press on" : "Continue";
        ShowNarrative(headline, text, resolution.Verdict, resolution.Changes, next, label, pendingNodeId);
    }

    private void ShowNarrative(
        string headline,
        string text,
        Verdict? verdict,
        List<ResourceChangeView> changes,
        NextStep next,
        string continueLabel,
        string? pendingNodeId = null,
        string? defeatReason = null)
    {
        state.Narrative = new PendingNarrative
        {
            Headline = headline,
            Text = text,
            Verdict = verdict,
            Changes = changes,
            Next = next,
            ContinueLabel = continueLabel,
            PendingNodeId = pendingNodeId,
            DefeatReason = defeatReason,
        };
        state.Phase = GamePhase.Narrative;
    }

    private void ApplyWorldFlavor(WorldFlavor flavor)
    {
        foreach (NodeFlavor node in flavor.Nodes)
        {
            MapNode? target = state.Map.FindNode(node.Id);

            // Origin and destination keep their canonical names; the LLM only flavors the trail between them.
            if (target is null || target.Kind is NodeKind.Origin or NodeKind.Destination)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(node.Name))
            {
                target.Name = node.Name.Trim();
            }

            if (!string.IsNullOrWhiteSpace(node.Description))
            {
                target.Description = node.Description.Trim();
            }
        }
    }

    private void Touch()
    {
        state.Version++;
        state.UpdatedAtUtc = Now;
    }

    private static DateTime? Earliest(DateTime? current, DateTime candidate) =>
        current is null || candidate < current ? candidate : current;

    private static string DescribeChanges(IEnumerable<ResourceChangeView> changes)
    {
        List<string> parts = [.. changes.Where(c => c.Delta != 0).Select(c => $"{c.Label} {c.Delta.ToString("+0;-0", CultureInfo.InvariantCulture)}")];
        return parts.Count == 0 ? "nothing gained or lost" : string.Join(", ", parts);
    }

    private static string Plural(int count, string singular, string? plural = null) =>
        $"{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? singular : plural ?? singular + "s")}";
}
