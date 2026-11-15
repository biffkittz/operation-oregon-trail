using Oregon.Contracts.Model;

namespace Oregon.Worker.Activities.Models;

/// <summary>The judge's ruling. The engine (not the LLM) turns the verdict into bounded consequences.</summary>
/// <param name="Verdict">Five-tier verdict.</param>
/// <param name="Score">0-100 quality score.</param>
/// <param name="Reasoning">One out-of-character sentence explaining the ruling.</param>
/// <param name="Narrative">What happens, in second person.</param>
/// <param name="NpcReply">The character's reply (empty when none).</param>
/// <param name="ManipulationDetected">True when the response tried to instruct the judge.</param>
public sealed record EvaluationResult(
    Verdict Verdict,
    int Score,
    string Reasoning,
    string Narrative,
    string NpcReply,
    bool ManipulationDetected);
