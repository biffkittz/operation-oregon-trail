using Oregon.Backend.Configuration;
using Temporalio.Client;

namespace Oregon.Backend.Infrastructure;

/// <summary>Maps <see cref="TemporalSettings"/> onto Temporal client connection options.</summary>
public static class TemporalConnection
{
    /// <summary>Configures target, namespace and (for Temporal Cloud) API key + TLS.</summary>
    /// <param name="options">Options to mutate.</param>
    /// <param name="settings">Settings from configuration.</param>
    public static void Configure(TemporalClientConnectOptions options, TemporalSettings settings)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(settings);
        options.TargetHost = settings.Address;
        options.Namespace = settings.Namespace;
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            options.ApiKey = settings.ApiKey;
            options.Tls = new TlsOptions();
        }
    }
}
