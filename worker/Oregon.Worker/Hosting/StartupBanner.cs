using Microsoft.Extensions.Options;
using Oregon.Worker.Configuration;

namespace Oregon.Worker.Hosting;

/// <summary>Logs the effective (non-secret) configuration once at startup, which makes misconfiguration obvious.</summary>
internal sealed partial class StartupBanner(
    IOptions<TemporalSettings> temporal,
    IOptions<LlmSettings> llm,
    ActiveLlmProvider provider,
    ILogger<StartupBanner> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        TemporalSettings t = temporal.Value;
        LlmSettings l = llm.Value;
        LogBanner(logger, t.Address, t.Namespace, t.TaskQueue, !string.IsNullOrWhiteSpace(t.ApiKey), provider.Provider.ToString(), l.Gemini.Judge.Model, l.Gemini.Narrator.Model);
        if (provider.Provider == LlmProvider.Fake)
        {
            LogFakeLlm(logger);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Oregon worker: Temporal {Address} namespace {Namespace} queue {TaskQueue} (api key: {HasApiKey}); LLM {Provider} (judge {JudgeModel}, narrator {NarratorModel})")]
    private static partial void LogBanner(ILogger logger, string address, string @namespace, string taskQueue, bool hasApiKey, string provider, string judgeModel, string narratorModel);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Using the offline FAKE LLM: narration is canned. Set Llm:Gemini:ApiKey (user-secrets) to play with Gemini.")]
    private static partial void LogFakeLlm(ILogger logger);
}
