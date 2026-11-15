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

    /// <summary>
    /// Gets or sets the SHA-256 thumbprint of our Cloudflare origin-pull client certificate. When set, requests
    /// without that certificate in <c>X-Forwarded-Client-Cert</c> are refused (see
    /// <see cref="Infrastructure.OriginCertificateMiddleware"/>). Not a secret: it identifies a public certificate.
    /// </summary>
    public string? OriginClientCertificateSha256 { get; set; }
}
