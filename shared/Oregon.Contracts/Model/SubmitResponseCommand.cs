namespace Oregon.Contracts.Model;

/// <summary>Answer the active free-text challenge in character.</summary>
/// <param name="Text">The captain's words or actions (untrusted input; treated as fiction by the LLM judge).</param>
public sealed record SubmitResponseCommand(string Text);
