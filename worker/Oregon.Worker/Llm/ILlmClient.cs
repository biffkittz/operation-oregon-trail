using System.Text.Json;

namespace Oregon.Worker.Llm;

/// <summary>Provider-neutral structured-output LLM client.</summary>
public interface ILlmClient
{
    /// <summary>Sends a prompt and returns the model's JSON object, already validated as well-formed JSON.</summary>
    /// <param name="request">Prompt, schema and sampling settings.</param>
    /// <param name="cancellationToken">Cancellation (wired to Temporal activity cancellation).</param>
    /// <returns>The root JSON element of the model's response.</returns>
    /// <exception cref="LlmException">The provider failed; <see cref="LlmException.IsTransient"/> says whether a retry may help.</exception>
    Task<JsonElement> GenerateJsonAsync(LlmRequest request, CancellationToken cancellationToken);
}
