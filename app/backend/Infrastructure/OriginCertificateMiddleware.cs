using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using Oregon.Backend.Configuration;

namespace Oregon.Backend.Infrastructure;

/// <summary>
/// Accepts a request only if it arrived through Cloudflare for <b>our</b> zone: Cloudflare presents our own origin
/// pull client certificate (zone-level Authenticated Origin Pulls), the Container Apps ingress requires a client
/// certificate and forwards it in <c>X-Forwarded-Client-Cert</c>, and this middleware compares its SHA-256
/// thumbprint with <see cref="ProxySettings.OriginClientCertificateSha256"/>.
/// </summary>
/// <remarks>
/// <para>Why the app checks: the ingress accepts any certificate (even self-signed) and leaves validation to the
/// app; and the IP allow-list admits all of Cloudflare, i.e. every Cloudflare customer could point a zone at our
/// static IP. Only our zone presents this certificate.</para>
/// <para>Health probes come from the platform directly to the container (no ingress, no certificate), so
/// <c>/healthz</c> is exempt; it reveals nothing.</para>
/// </remarks>
public sealed partial class OriginCertificateMiddleware(RequestDelegate next, IOptions<ProxySettings> settings, ILogger<OriginCertificateMiddleware> logger)
{
    /// <summary>Header the Container Apps ingress uses to forward the client certificate.</summary>
    public const string HeaderName = "X-Forwarded-Client-Cert";

    /// <summary>Processes a request.</summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        string? pin = Normalize(settings.Value.OriginClientCertificateSha256);
        if (pin is null || context.Request.Path.StartsWithSegments("/healthz", StringComparison.OrdinalIgnoreCase))
        {
            return next(context);
        }

        string? presented = context.Request.Headers[HeaderName] is { Count: 1 } values ? Thumbprint(values[0]!) : null;
        if (presented is not null && CryptographicOperations.FixedTimeEquals(Convert.FromHexString(presented), Convert.FromHexString(pin)))
        {
            return next(context);
        }

        LogRejected(logger, presented ?? "none");
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    /// <summary>
    /// SHA-256 thumbprint (lower-case hex) of the single certificate in an XFCC value, computed from its <c>Cert</c>
    /// field when present (URL-encoded or raw PEM), otherwise taken from <c>Hash</c>. Null when the header names
    /// more than one certificate (a comma-separated chain of hops) or none.
    /// </summary>
    /// <param name="xfcc">Header value.</param>
    /// <returns>The thumbprint, or null.</returns>
    public static string? Thumbprint(string xfcc)
    {
        ArgumentNullException.ThrowIfNull(xfcc);
        List<string> elements = SplitOutsideQuotes(xfcc, ',');
        if (elements.Count != 1)
        {
            return null;
        }

        string? hash = null;
        foreach (string pair in SplitOutsideQuotes(elements[0], ';'))
        {
            int equals = pair.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0)
            {
                continue;
            }

            string key = pair[..equals].Trim();
            string value = pair[(equals + 1)..].Trim().Trim('"');
            if (key.Equals("Cert", StringComparison.OrdinalIgnoreCase) && FromPem(value) is { } fromCert)
            {
                return fromCert;
            }

            if (key.Equals("Hash", StringComparison.OrdinalIgnoreCase))
            {
                hash = Normalize(value);
            }
        }

        return hash;
    }

    /// <summary>Lower-case hex without separators, or null when the input isn't a 32-byte hex digest.</summary>
    /// <param name="value">A thumbprint in any common notation (colons, spaces, either case).</param>
    /// <returns>The normalized thumbprint, or null.</returns>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string hex = new([.. value.Where(Uri.IsHexDigit)]);
        return hex.Length == 64 ? hex.ToLowerInvariant() : null;
    }

    private static string? FromPem(string value)
    {
        try
        {
            using var certificate = X509Certificate2.CreateFromPem(Uri.UnescapeDataString(value));
            return certificate.GetCertHashString(HashAlgorithmName.SHA256).ToLowerInvariant();
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static List<string> SplitOutsideQuotes(string value, char separator)
    {
        List<string> parts = [];
        bool quoted = false;
        int start = 0;
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '"')
            {
                quoted = !quoted;
            }
            else if (value[i] == separator && !quoted)
            {
                parts.Add(value[start..i]);
                start = i + 1;
            }
        }

        parts.Add(value[start..]);
        return [.. parts.Where(p => !string.IsNullOrWhiteSpace(p))];
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected a request that did not come through our Cloudflare zone (client certificate {Thumbprint})")]
    private static partial void LogRejected(ILogger logger, string thumbprint);
}
