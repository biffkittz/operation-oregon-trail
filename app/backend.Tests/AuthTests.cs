using System.Net;
using System.Net.Http.Json;
using Oregon.Backend.Auth;
using Oregon.Backend.Tests.Support;

namespace Oregon.Backend.Tests;

public sealed class AuthTests(BackendFactory factory) : ApiTestBase(factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ApiRequiresASession()
    {
        HttpResponseMessage response = await Client().GetAsync("/api/games", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongPassword_IsRejected()
    {
        HttpResponseMessage response = await Client().PostAsJsonAsync("/api/auth/login", new { password = "nope" }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_SetsAHardenedCookie_AndLogoutEndsTheSession()
    {
        HttpClient client = Client();

        HttpResponseMessage login = await client.PostAsJsonAsync("/api/auth/login", new { password = BackendFactory.Password }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        string cookie = login.Headers.GetValues("Set-Cookie").Single();
        Assert.StartsWith("__Host-oregon_session=", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.True((await client.GetFromJsonAsync<SessionResponse>("/api/auth/session", Ct))!.Authenticated);

        await client.PostAsync("/api/auth/logout", null, Ct);

        Assert.False((await client.GetFromJsonAsync<SessionResponse>("/api/auth/session", Ct))!.Authenticated);
    }

    [Fact]
    public async Task Login_IsRateLimitedPerClientIp()
    {
        HttpClient attacker = Client("203.0.113.7");
        List<HttpStatusCode> statuses = [];
        for (int i = 0; i < 7; i++)
        {
            statuses.Add((await attacker.PostAsJsonAsync("/api/auth/login", new { password = $"guess-{i}" }, Ct)).StatusCode);
        }

        Assert.Equal(5, statuses.Count(s => s == HttpStatusCode.Unauthorized));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);

        // A different client is unaffected.
        HttpResponseMessage other = await Client("198.51.100.9").PostAsJsonAsync("/api/auth/login", new { password = BackendFactory.Password }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, other.StatusCode);
    }

    [Fact]
    public async Task Responses_CarrySecurityHeaders()
    {
        HttpResponseMessage response = await Client().GetAsync("/api/auth/session", Ct);

        string csp = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("default-src 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", csp, StringComparison.Ordinal);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Theory]
    [InlineData("correct horse", "correct horse", true)]
    [InlineData("correct horse", "correct horsE", false)]
    [InlineData("correct horse", "", false)]
    [InlineData("correct horse", null, false)]
    public void PasswordVerifier_ComparesExactly(string configured, string? candidate, bool expected)
    {
        Microsoft.Extensions.Options.IOptionsMonitor<Configuration.AuthSettings> monitor =
            new StaticOptionsMonitor<Configuration.AuthSettings>(new Configuration.AuthSettings { Password = configured });

        Assert.Equal(expected, new PasswordVerifier(monitor).Verify(candidate));
    }
}
