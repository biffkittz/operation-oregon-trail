namespace Oregon.Backend.Configuration;

/// <summary>Reverse-proxy settings.</summary>
public sealed class ProxySettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Proxy";

    /// <summary>
    /// Gets or sets a value indicating whether to trust <c>CF-Connecting-IP</c> as the client address. Only safe
    /// because Container Apps ingress is IP-restricted to Cloudflare's ranges, so the header cannot be spoofed.
    /// </summary>
    public bool TrustCloudflareHeaders { get; set; }
}
