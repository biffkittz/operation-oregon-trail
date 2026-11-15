using Oregon.Worker.Configuration;

namespace Oregon.Worker.Hosting;

/// <summary>The LLM provider actually in use after resolving <see cref="LlmProvider.Auto"/>.</summary>
/// <param name="Provider">Resolved provider.</param>
public sealed record ActiveLlmProvider(LlmProvider Provider);
