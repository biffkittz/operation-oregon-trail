using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Oregon.Backend.Data;

namespace Oregon.Backend.Tests.Support;

/// <summary>Resets the database before each test and provides clients.</summary>
[Collection(nameof(ApiCollection))]
public abstract class ApiTestBase(BackendFactory factory) : IAsyncLifetime
{
    protected BackendFactory Factory => factory;

    public async ValueTask InitializeAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        OregonDbContext db = scope.ServiceProvider.GetRequiredService<OregonDbContext>();
        await db.Games.ExecuteDeleteAsync();
        await db.Settings.ExecuteDeleteAsync();
        factory.Engine.Started.Clear();
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// The session cookie is "__Host-" prefixed and Secure outside Development, so clients must use https.
    /// Each client gets its own forwarded IP so rate-limit partitions never leak between tests.
    /// </summary>
    protected HttpClient Client(string? clientIp = null)
    {
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
        });
        client.DefaultRequestHeaders.Add("X-Forwarded-For", clientIp ?? $"10.0.{Random.Shared.Next(255)}.{Random.Shared.Next(1, 255)}");
        return client;
    }

    protected async Task<HttpClient> SignedInClientAsync()
    {
        HttpClient client = Client();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/login", new { password = BackendFactory.Password }, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return client;
    }
}
