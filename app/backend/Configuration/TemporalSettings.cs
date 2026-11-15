using System.ComponentModel.DataAnnotations;
using Oregon.Contracts;

namespace Oregon.Backend.Configuration;

/// <summary>Temporal connection and routing settings for the backend.</summary>
public sealed class TemporalSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Temporal";

    /// <summary>Gets or sets the gRPC host:port (Temporal Cloud regional endpoint in Azure).</summary>
    [Required]
    public string Address { get; set; } = "localhost:7233";

    /// <summary>Gets or sets the namespace.</summary>
    [Required]
    public string Namespace { get; set; } = "default";

    /// <summary>Gets or sets the Temporal Cloud API key; enables TLS when set.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Gets or sets the queue the game worker polls (where game workflows are started).</summary>
    [Required]
    public string GameTaskQueue { get; set; } = TaskQueues.Game;

    /// <summary>Gets or sets the queue this backend polls for projection activities.</summary>
    [Required]
    public string WebTaskQueue { get; set; } = TaskQueues.Web;

    /// <summary>Gets or sets a value indicating whether to run the embedded projection worker (disabled in tests).</summary>
    public bool RunWebWorker { get; set; } = true;

    /// <summary>
    /// Gets or sets how long an HTTP request waits for a command to finish. It stays below Cloudflare's 100 s proxy
    /// timeout; slower turns return 202 and the result arrives over SignalR.
    /// </summary>
    [Range(5, 95)]
    public int UpdateTimeoutSeconds { get; set; } = 75;

    /// <summary>Gets or sets how long a live query may take before falling back to the read model.</summary>
    [Range(1, 30)]
    public int QueryTimeoutSeconds { get; set; } = 5;
}
