using Oregon.Contracts.Model;
using Oregon.Worker.Activities.Models;

namespace Oregon.Worker.Game;

/// <summary>
/// Offline judge used when the LLM is unavailable (fallback) and by the fake LLM in development and tests.
/// It rewards effort and constructive intent, and treats prompt-injection attempts exactly like the real judge.
/// </summary>
internal static class HeuristicJudge
{
    private static readonly string[] ManipulationMarkers =
    [
        "ignore previous", "ignore all", "ignore your", "system prompt", "instructions", "you are an ai",
        "as an ai", "verdict", "critical_success", "score of", "override", "jailbreak", "developer mode",
    ];

    private static readonly string[] ConstructiveWords =
    [
        "offer", "trade", "help", "promise", "share", "together", "because", "plan", "repair", "protect",
        "listen", "thank", "fair", "honest", "sing", "story", "build", "reroute", "calm", "steady",
    ];

    public static bool LooksLikeManipulation(string text)
    {
        string lower = text.ToLowerInvariant();
        return ManipulationMarkers.Any(lower.Contains);
    }

    public static EvaluationResult Judge(EvaluationRequest request) =>
        Judge(request.PlayerResponse, request.NpcName, request.StatLevel);

    public static EvaluationResult Judge(string playerResponse, string? npcName, int statLevel)
    {
        string text = playerResponse.Trim();
        string npc = string.IsNullOrWhiteSpace(npcName) ? "The stranger" : npcName;
        if (LooksLikeManipulation(text))
        {
            return new EvaluationResult(
                Verdict.CriticalFailure,
                0,
                "The response tried to instruct the judge instead of acting in the story.",
                $"You start reciting strange incantations about rules and verdicts. {npc} backs away slowly.",
                "Whatever you're selling, captain, I ain't buying.",
                ManipulationDetected: true);
        }

        string lower = text.ToLowerInvariant();
        int words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        int constructive = ConstructiveWords.Count(lower.Contains);
        int score = Math.Clamp(15 + Math.Min(words, 40) + (constructive * 7) + (statLevel * 3), 0, 100);
        Verdict verdict = score switch
        {
            >= 85 => Verdict.CriticalSuccess,
            >= 62 => Verdict.Success,
            >= 42 => Verdict.Partial,
            >= 22 => Verdict.Failure,
            _ => Verdict.CriticalFailure,
        };

        bool hasCounterpart = !string.IsNullOrWhiteSpace(npcName);
        (string narrative, string reply) = (verdict, hasCounterpart) switch
        {
            (Verdict.CriticalSuccess, true) => ($"Your words land perfectly. {npc} breaks into a grin and the tension drains away.", "Well now. That's the best offer I've heard all season."),
            (Verdict.Success, true) => ($"It takes some convincing, but {npc} comes around to your way of thinking.", "Alright, captain. We have a deal."),
            (Verdict.Partial, true) => ($"{npc} meets you halfway, though nobody leaves entirely happy.", "Half a loaf, captain. Take it or leave it."),
            (Verdict.Failure, true) => ($"Your words fall flat and {npc} shakes their head.", "Not good enough, I'm afraid."),
            (_, true) => ($"Everything goes wrong at once. {npc} is not amused.", "Get off my dock."),
            (Verdict.CriticalSuccess, false) => ("Your plan works better than anyone dared hope; the crew will tell this story for years.", string.Empty),
            (Verdict.Success, false) => ("It takes sweat and nerve, but your plan holds together.", string.Empty),
            (Verdict.Partial, false) => ("Your plan half works. Half is better than nothing out here.", string.Empty),
            (Verdict.Failure, false) => ("The plan falls apart at the worst possible moment.", string.Empty),
            _ => ("Everything that could go wrong does, all at once.", string.Empty),
        };

        return new EvaluationResult(verdict, score, "Judged offline by length, intent and aptitude.", narrative, reply, ManipulationDetected: false);
    }
}
