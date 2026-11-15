using Microsoft.Extensions.Options;
using Oregon.Worker.Activities;
using Oregon.Worker.Configuration;
using Oregon.Worker.Llm;
using Oregon.Worker.Workflows;
using Temporalio.Extensions.Hosting;

namespace Oregon.Worker.Hosting;

/// <summary>Registers everything the worker needs: settings, the LLM client and the hosted Temporal worker.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Adds the Oregon game worker to the service collection.</summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="environment">Host environment.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddOregonWorker(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddOptions<TemporalSettings>()
            .Bind(configuration.GetSection(TemporalSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<LlmSettings>()
            .Bind(configuration.GetSection(LlmSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Registration-time decisions (which LLM client, which task queue) need the values now, not lazily.
        LlmSettings llm = configuration.GetSection(LlmSettings.SectionName).Get<LlmSettings>() ?? new LlmSettings();
        LlmProvider provider = ResolveProvider(llm);
        services.AddSingleton(new ActiveLlmProvider(provider));
        if (provider == LlmProvider.Fake)
        {
            services.AddSingleton<ILlmClient, FakeLlmClient>();
        }
        else
        {
            services.AddHttpClient<ILlmClient, GeminiLlmClient>(client =>
            {
                client.BaseAddress = llm.Gemini.BaseUrl;
                client.Timeout = TimeSpan.FromSeconds(llm.TimeoutSeconds);
            });
        }

        TemporalSettings temporal = configuration.GetSection(TemporalSettings.SectionName).Get<TemporalSettings>() ?? new TemporalSettings();
        services.AddTemporalClient(options => TemporalConnection.Configure(options, temporal));
        services.AddHostedTemporalWorker(temporal.TaskQueue)
            .AddScopedActivities<NarrativeActivities>()
            .AddWorkflow<GameWorkflow>()
            .AddWorkflow<StorySeedWorkflow>();

        services.AddHostedService<StartupBanner>();
        return services;
    }

    /// <summary>
    /// "Auto" lets contributors run without a Gemini key (fake LLM) while production config pins "Gemini" so a
    /// missing secret fails fast at startup instead of silently serving canned prose.
    /// </summary>
    private static LlmProvider ResolveProvider(LlmSettings settings) => settings.Provider switch
    {
        LlmProvider.Gemini when string.IsNullOrWhiteSpace(settings.Gemini.ApiKey) =>
            throw new OptionsValidationException(
                LlmSettings.SectionName,
                typeof(LlmSettings),
                ["Llm:Provider is Gemini but Llm:Gemini:ApiKey is empty. Set the key or use Provider=Auto/Fake."]),
        LlmProvider.Auto => string.IsNullOrWhiteSpace(settings.Gemini.ApiKey) ? LlmProvider.Fake : LlmProvider.Gemini,
        _ => settings.Provider,
    };
}
