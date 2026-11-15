namespace Oregon.Backend.Infrastructure;

/// <summary>
/// Adds defensive headers to every response. The CSP allows only same-origin scripts, styles, fonts and
/// connections (SignalR WebSockets included): the SPA bundles everything, including fonts, so it needs no CDN.
/// </summary>
/// <remarks>
/// <c>style-src 'self'</c> works because Vue applies dynamic styles through the CSSOM, which CSP does not govern;
/// templates must therefore avoid static <c>style="..."</c> attributes.
/// </remarks>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; font-src 'self'; " +
        "connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; object-src 'none'";

    /// <summary>Adds the headers and calls the next middleware.</summary>
    /// <param name="context">Current request.</param>
    /// <returns>A task.</returns>
    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        IHeaderDictionary headers = context.Response.Headers;
        headers.ContentSecurityPolicy = ContentSecurityPolicy;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        return next(context);
    }
}
