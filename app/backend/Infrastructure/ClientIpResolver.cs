using System.Net;
using Microsoft.Extensions.Options;
using Oregon.Backend.Configuration;

namespace Oregon.Backend.Infrastructure;

/// <summary>Works out the real client IP behind Cloudflare and the Container Apps ingress.</summary>
public sealed class ClientIpResolver(IOptions<ProxySettings> settings)
{
    /// <summary>Header Cloudflare sets to the visitor's IP.</summary>
    public const string CloudflareHeader = "CF-Connecting-IP";

    /// <summary>Returns the client IP used as the rate-limiting partition key.</summary>
    /// <param name="context">Current request.</param>
    /// <returns>An IP address string, or "unknown".</returns>
    public string Resolve(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (settings.Value.TrustCloudflareHeaders
            && context.Request.Headers.TryGetValue(CloudflareHeader, out Microsoft.Extensions.Primitives.StringValues header)
            && IPAddress.TryParse(header.ToString(), out IPAddress? cloudflareIp))
        {
            return cloudflareIp.ToString();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
