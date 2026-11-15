using System.Text.Json;

namespace Oregon.Worker.Llm;

/// <summary>JSON conventions for LLM payloads: snake_case keys, matching the prompt format in the design brief.</summary>
internal static class LlmJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false,
    };
}
