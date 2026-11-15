using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Headers;
using Microsoft.Net.Http.Headers;
using Oregon.Backend.Auth;
using Oregon.Backend.Games;
using Oregon.Backend.Realtime;
using Oregon.Backend.Settings;

namespace Oregon.Backend.Infrastructure;

/// <summary>Builds the HTTP pipeline and maps endpoints.</summary>
public static class BackendPipeline
{
    /// <summary>Configures middleware and endpoints.</summary>
    /// <param name="app">The web application.</param>
    /// <returns>The same application.</returns>
    public static WebApplication UseOregonBackend(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        StaticFileOptions staticFiles = new() { OnPrepareResponse = context => SetCacheHeaders(context.Context, context.File.Name) };

        // First: anything that did not come through our Cloudflare zone stops here (no-op when no pin is configured).
        app.UseMiddleware<OriginCertificateMiddleware>();
        app.UseForwardedHeaders();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseStaticFiles(staticFiles);
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        RouteGroupBuilder api = app.MapGroup("/api").RequireAuthorization();
        api.MapAuthEndpoints();
        api.MapGameEndpoints();
        api.MapSettingsEndpoints();

        // Unknown API routes must 404 instead of falling through to the SPA's index.html.
        api.Map("/{**path}", () => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "No such API route")).AllowAnonymous();

        app.MapHub<GameHub>(GameHub.Path);
        app.MapHealthChecks("/healthz/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks("/healthz/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        // Client-side routes (/game/..., /settings) are served the SPA shell.
        app.MapFallbackToFile("index.html", staticFiles).AllowAnonymous();
        return app;
    }

    /// <summary>
    /// Vite fingerprints everything under /assets, so those files can be cached forever; index.html must always be
    /// revalidated so a new deployment is picked up immediately.
    /// </summary>
    private static void SetCacheHeaders(HttpContext context, string fileName)
    {
        ResponseHeaders headers = context.Response.GetTypedHeaders();
        if (string.Equals(fileName, "index.html", StringComparison.OrdinalIgnoreCase))
        {
            headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        }
        else if (context.Request.Path.StartsWithSegments("/assets", StringComparison.Ordinal))
        {
            // Build the value before assigning: the CacheControl getter returns a freshly parsed copy, so mutating
            // it after assignment would be silently lost.
            CacheControlHeaderValue immutable = new() { Public = true, MaxAge = TimeSpan.FromDays(365) };
            immutable.Extensions.Add(new NameValueHeaderValue("immutable"));
            headers.CacheControl = immutable;
        }
        else
        {
            headers.CacheControl = new CacheControlHeaderValue { Public = true, MaxAge = TimeSpan.FromHours(1) };
        }
    }
}
