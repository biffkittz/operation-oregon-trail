using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Oregon.Worker.Configuration;

namespace Oregon.Worker.Llm;

/// <summary>
/// Calls Gemini's <c>generateContent</c> REST endpoint with <c>responseJsonSchema</c> structured output.
/// </summary>
/// <remarks>
/// The REST API is used directly (rather than an SDK) to keep the dependency surface small and the wire format
/// obvious. There is deliberately no HTTP-level retry: Temporal's activity retry policy is the single retry layer.
/// </remarks>
internal sealed partial class GeminiLlmClient(
    HttpClient httpClient,
    IOptions<LlmSettings> options,
    ILogger<GeminiLlmClient> logger) : ILlmClient
{
    public async Task<JsonElement> GenerateJsonAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        GeminiSettings gemini = options.Value.Gemini;
        GeminiModelSettings model = request.Purpose == LlmPurpose.Judge ? gemini.Judge : gemini.Narrator;

        using HttpRequestMessage message = new(HttpMethod.Post, $"v1beta/models/{Uri.EscapeDataString(model.Model)}:generateContent")
        {
            Content = new StringContent(BuildBody(request, model).ToJsonString(), Encoding.UTF8, "application/json"),
        };
        message.Headers.Add("x-goog-api-key", gemini.ApiKey);

        long started = Stopwatch.GetTimestamp();
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(message, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new LlmException($"Gemini request failed: {ex.Message}", isTransient: true);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new LlmException("Gemini request timed out.", isTransient: true);
        }

        using (response)
        {
            string payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw Classify(response, payload);
            }

            JsonElement result = ExtractJson(payload, out JsonElement usage);
            LogCompletion(logger, request.SchemaName, model.Model, Stopwatch.GetElapsedTime(started).TotalMilliseconds, Token(usage, "promptTokenCount"), Token(usage, "candidatesTokenCount"), Token(usage, "thoughtsTokenCount"));
            return result;
        }
    }

    private static JsonObject BuildBody(LlmRequest request, GeminiModelSettings model)
    {
        JsonObject generationConfig = new()
        {
            ["temperature"] = request.Temperature,
            ["maxOutputTokens"] = request.MaxOutputTokens,
            ["responseMimeType"] = "application/json",
            ["responseJsonSchema"] = JsonNode.Parse(request.ResponseSchemaJson),
        };
        if (!string.IsNullOrWhiteSpace(model.ThinkingLevel))
        {
            generationConfig["thinkingConfig"] = new JsonObject { ["thinkingLevel"] = model.ThinkingLevel };
        }

        return new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = request.SystemInstruction }) },
            ["contents"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = request.UserPayloadJson }),
            }),
            ["generationConfig"] = generationConfig,
        };
    }

    private static JsonElement ExtractJson(string payload, out JsonElement usage)
    {
        using var document = JsonDocument.Parse(payload);
        JsonElement root = document.RootElement;
        usage = root.TryGetProperty("usageMetadata", out JsonElement u) ? u.Clone() : default;

        if (!root.TryGetProperty("candidates", out JsonElement candidates) || candidates.GetArrayLength() == 0)
        {
            string reason = root.TryGetProperty("promptFeedback", out JsonElement feedback) && feedback.TryGetProperty("blockReason", out JsonElement block)
                ? block.GetString() ?? "unknown"
                : "no candidates";
            throw new LlmException($"Gemini returned no answer ({reason}).", isTransient: false);
        }

        JsonElement candidate = candidates[0];
        string finishReason = candidate.TryGetProperty("finishReason", out JsonElement finish) ? finish.GetString() ?? string.Empty : string.Empty;
        if (finishReason is "SAFETY" or "RECITATION" or "BLOCKLIST" or "PROHIBITED_CONTENT" or "SPII")
        {
            throw new LlmException($"Gemini declined to answer ({finishReason}).", isTransient: false);
        }

        StringBuilder text = new();
        if (candidate.TryGetProperty("content", out JsonElement content) && content.TryGetProperty("parts", out JsonElement parts))
        {
            foreach (JsonElement part in parts.EnumerateArray())
            {
                bool isThought = part.TryGetProperty("thought", out JsonElement thought) && thought.ValueKind == JsonValueKind.True;
                if (!isThought && part.TryGetProperty("text", out JsonElement value))
                {
                    text.Append(value.GetString());
                }
            }
        }

        try
        {
            using var answer = JsonDocument.Parse(text.ToString());
            return answer.RootElement.ValueKind == JsonValueKind.Object
                ? answer.RootElement.Clone()
                : throw new LlmException("Gemini returned JSON that is not an object.", isTransient: true);
        }
        catch (JsonException ex)
        {
            // Typically a MAX_TOKENS truncation; a fresh attempt usually succeeds.
            throw new LlmException($"Gemini returned malformed JSON (finish reason {finishReason}): {ex.Message}", isTransient: true);
        }
    }

    private static LlmException Classify(HttpResponseMessage response, string payload)
    {
        string detail = payload.Length > 300 ? payload[..300] : payload;
        TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta;
        return response.StatusCode switch
        {
            HttpStatusCode.TooManyRequests or HttpStatusCode.RequestTimeout or >= HttpStatusCode.InternalServerError
                => new LlmException($"Gemini is temporarily unavailable ({(int)response.StatusCode}): {detail}", isTransient: true, retryAfter),
            _ => new LlmException($"Gemini rejected the request ({(int)response.StatusCode}): {detail}", isTransient: false),
        };
    }

    private static int Token(JsonElement usage, string name) =>
        usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int count) ? count : 0;

    [LoggerMessage(Level = LogLevel.Information, Message = "Gemini {SchemaName} via {Model} in {ElapsedMs:F0} ms (tokens: prompt {PromptTokens}, output {OutputTokens}, thinking {ThinkingTokens})")]
    private static partial void LogCompletion(ILogger logger, string schemaName, string model, double elapsedMs, int promptTokens, int outputTokens, int thinkingTokens);
}
