using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Oregon.Worker.Configuration;
using Oregon.Worker.Llm;
using Oregon.Worker.Tests.Support;

namespace Oregon.Worker.Tests.Llm;

public sealed class GeminiLlmClientTests
{
    private const string Schema = """{"type":"object","properties":{"text":{"type":"string"}},"required":["text"]}""";

    [Fact]
    public async Task SendsStructuredOutputRequest_ToTheJudgeModelWithThinking()
    {
        StubHttpHandler handler = new(HttpStatusCode.OK, Candidate("""{"text":"hi"}"""));
        GeminiLlmClient client = Client(handler);

        JsonElement result = await client.GenerateJsonAsync(Request(LlmPurpose.Judge), TestContext.Current.CancellationToken);

        Assert.Equal("hi", result.GetProperty("text").GetString());
        Assert.EndsWith("v1beta/models/judge-model:generateContent", handler.LastRequest!.RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.Equal("test-key", handler.LastRequest.Headers.GetValues("x-goog-api-key").Single());
        using var body = JsonDocument.Parse(handler.LastBody!);
        JsonElement config = body.RootElement.GetProperty("generationConfig");
        Assert.Equal("application/json", config.GetProperty("responseMimeType").GetString());
        Assert.Equal("object", config.GetProperty("responseJsonSchema").GetProperty("type").GetString());
        Assert.Equal("low", config.GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
    }

    [Fact]
    public async Task NarratorModelWithoutThinkingLevel_OmitsThinkingConfig()
    {
        StubHttpHandler handler = new(HttpStatusCode.OK, Candidate("""{"text":"hi"}"""));

        await Client(handler).GenerateJsonAsync(Request(LlmPurpose.Narrator), TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.False(body.RootElement.GetProperty("generationConfig").TryGetProperty("thinkingConfig", out _));
        Assert.Contains("narrator-model", handler.LastRequest!.RequestUri!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThoughtParts_AreIgnored()
    {
        string payload = """
            {"candidates":[{"finishReason":"STOP","content":{"parts":[{"text":"thinking...","thought":true},{"text":"{\"text\":\"answer\"}"}]}}]}
            """;

        JsonElement result = await Client(new StubHttpHandler(HttpStatusCode.OK, payload))
            .GenerateJsonAsync(Request(LlmPurpose.Judge), TestContext.Current.CancellationToken);

        Assert.Equal("answer", result.GetProperty("text").GetString());
    }

    [Fact]
    public async Task RateLimit_IsTransientAndCarriesRetryAfter()
    {
        StubHttpHandler handler = new(HttpStatusCode.TooManyRequests, "{}", TimeSpan.FromSeconds(7));

        LlmException ex = await Assert.ThrowsAsync<LlmException>(() =>
            Client(handler).GenerateJsonAsync(Request(LlmPurpose.Judge), TestContext.Current.CancellationToken));

        Assert.True(ex.IsTransient);
        Assert.Equal(TimeSpan.FromSeconds(7), ex.RetryAfter);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task ClientErrors_ArePermanent(HttpStatusCode status)
    {
        LlmException ex = await Assert.ThrowsAsync<LlmException>(() =>
            Client(new StubHttpHandler(status, """{"error":"nope"}""")).GenerateJsonAsync(Request(LlmPurpose.Judge), TestContext.Current.CancellationToken));

        Assert.False(ex.IsTransient);
    }

    [Fact]
    public async Task SafetyBlock_IsPermanent()
    {
        string payload = """{"candidates":[{"finishReason":"SAFETY","content":{"parts":[]}}]}""";

        LlmException ex = await Assert.ThrowsAsync<LlmException>(() =>
            Client(new StubHttpHandler(HttpStatusCode.OK, payload)).GenerateJsonAsync(Request(LlmPurpose.Judge), TestContext.Current.CancellationToken));

        Assert.False(ex.IsTransient);
    }

    [Fact]
    public async Task TruncatedJson_IsTransient()
    {
        string payload = """{"candidates":[{"finishReason":"MAX_TOKENS","content":{"parts":[{"text":"{\"text\":\"unfinish"}]}}]}""";

        LlmException ex = await Assert.ThrowsAsync<LlmException>(() =>
            Client(new StubHttpHandler(HttpStatusCode.OK, payload)).GenerateJsonAsync(Request(LlmPurpose.Judge), TestContext.Current.CancellationToken));

        Assert.True(ex.IsTransient);
    }

    private static GeminiLlmClient Client(StubHttpHandler handler)
    {
        LlmSettings settings = new()
        {
            Gemini = new GeminiSettings
            {
                ApiKey = "test-key",
                Judge = new GeminiModelSettings { Model = "judge-model", ThinkingLevel = "low" },
                Narrator = new GeminiModelSettings { Model = "narrator-model", ThinkingLevel = "" },
            },
        };
        HttpClient http = new(handler) { BaseAddress = new Uri("https://example.test/") };
        return new GeminiLlmClient(http, Options.Create(settings), NullLogger<GeminiLlmClient>.Instance);
    }

    private static LlmRequest Request(LlmPurpose purpose) => new(purpose, "test", "system", """{"llm_task_description":"x"}""", Schema, 0.5, 256);

    private static string Candidate(string json) =>
        JsonSerializer.Serialize(new { candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { text = json } } } } } });
}
