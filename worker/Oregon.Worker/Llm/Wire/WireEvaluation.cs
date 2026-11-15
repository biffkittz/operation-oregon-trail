namespace Oregon.Worker.Llm.Wire;

/// <summary>Raw model output for a judgment.</summary>
internal sealed record WireEvaluation(
    string? Verdict,
    int? Score,
    string? Reasoning,
    string? Narrative,
    string? NpcReply,
    bool? ManipulationDetected);
