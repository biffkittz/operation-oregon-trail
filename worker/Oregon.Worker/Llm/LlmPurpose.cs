namespace Oregon.Worker.Llm;

/// <summary>Selects the model profile (quality vs. latency).</summary>
public enum LlmPurpose
{
    /// <summary>Judging free-text responses; favors reasoning quality.</summary>
    Judge,

    /// <summary>Writing prose and encounters; favors speed.</summary>
    Narrator,
}
