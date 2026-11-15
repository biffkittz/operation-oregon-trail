using System.ComponentModel.DataAnnotations;
using Oregon.Contracts;

namespace Oregon.Worker.Configuration;

/// <summary>
/// Connection settings for Temporal. Locally this points at the dev server; in Azure it points at
/// Temporal Cloud's regional endpoint with an API key (bound from the <c>Temporal__ApiKey</c> secret).
/// </summary>
public sealed class TemporalSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Temporal";

    /// <summary>Gets or sets the gRPC host:port, e.g. <c>us-east-2.aws.api.temporal.io:7233</c>.</summary>
    [Required]
    public string Address { get; set; } = "localhost:7233";

    /// <summary>Gets or sets the Temporal namespace, e.g. <c>oregon.joqqs</c>.</summary>
    [Required]
    public string Namespace { get; set; } = "default";

    /// <summary>Gets or sets the Temporal Cloud API key. When set, TLS is enabled automatically.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Gets or sets the task queue this worker polls.</summary>
    [Required]
    public string TaskQueue { get; set; } = TaskQueues.Game;
}
