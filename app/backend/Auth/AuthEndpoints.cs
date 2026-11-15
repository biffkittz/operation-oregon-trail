using System.Text;
using System.Security.Cryptography;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Oregon.Backend.Infrastructure;

namespace Oregon.Backend.Auth;

/// <summary>Single-password login using an HttpOnly session cookie (no tokens in browser storage).</summary>
public static class AuthEndpoints
{
    /// <summary>The only principal this app knows about.</summary>
    public const string CaptainName = "captain";

    /// <summary>Claim carrying <see cref="IPasswordVerifier.SessionStamp"/>; see <see cref="ValidateSessionAsync"/>.</summary>
    public const string PasswordStampClaim = "oregon:pwstamp";

    /// <summary>Maps <c>/api/auth/*</c>.</summary>
    /// <param name="api">The API route group.</param>
    /// <returns>The same route group.</returns>
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        RouteGroupBuilder auth = api.MapGroup("/auth").WithTags("Auth").AllowAnonymous();
        auth.MapPost("/login", LoginAsync).RequireRateLimiting(RateLimitPolicies.Login);
        auth.MapPost("/logout", (Delegate)LogoutAsync);
        auth.MapGet("/session", (HttpContext http) =>
            TypedResults.Ok(new SessionResponse(http.User.Identity?.IsAuthenticated == true)));
        return api;
    }

    /// <summary>
    /// Cookie validation hook: rejects sessions issued under a different password (or before stamps existed), so a
    /// password change logs everyone out even though the cookie-signing keys survive restarts.
    /// </summary>
    /// <param name="context">The cookie validation context.</param>
    /// <returns>A task.</returns>
    public static async Task ValidateSessionAsync(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        IPasswordVerifier verifier = context.HttpContext.RequestServices.GetRequiredService<IPasswordVerifier>();
        string? stamp = context.Principal?.FindFirst(PasswordStampClaim)?.Value;
        byte[] presented = Encoding.UTF8.GetBytes(stamp ?? string.Empty);
        byte[] current = Encoding.UTF8.GetBytes(verifier.SessionStamp());
        if (stamp is null || !CryptographicOperations.FixedTimeEquals(presented, current))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> LoginAsync(
        LoginRequest request,
        IPasswordVerifier verifier,
        LoginThrottle throttle,
        HttpContext http)
    {
        // Site-wide brake on guessing from many IPs (the per-IP rate limit only slows a single address). While it is
        // engaged, every attempt is refused, including correct ones: a minute of lockout beats a guessed password.
        if (throttle.IsEngaged())
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Too many failed sign-ins",
                detail: "The airlock is sealed for a minute after too many wrong passwords. Try again shortly.");
        }

        if (!verifier.Verify(request.Password))
        {
            throttle.RecordFailure();
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Wrong password", detail: "The airlock stays shut.");
        }

        ClaimsIdentity identity = new(
            [new Claim(ClaimTypes.Name, CaptainName), new Claim(PasswordStampClaim, verifier.SessionStamp())],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> LogoutAsync(HttpContext http)
    {
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return TypedResults.NoContent();
    }
}
