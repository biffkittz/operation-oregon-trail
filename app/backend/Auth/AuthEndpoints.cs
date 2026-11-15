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

    private static async Task<Results<NoContent, ProblemHttpResult>> LoginAsync(LoginRequest request, IPasswordVerifier verifier, HttpContext http)
    {
        if (!verifier.Verify(request.Password))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Wrong password", detail: "The airlock stays shut.");
        }

        ClaimsIdentity identity = new([new Claim(ClaimTypes.Name, CaptainName)], CookieAuthenticationDefaults.AuthenticationScheme);
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
