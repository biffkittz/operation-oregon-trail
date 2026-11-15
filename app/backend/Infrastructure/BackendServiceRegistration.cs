using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Oregon.Backend.Auth;
using Oregon.Backend.Configuration;
using Oregon.Backend.Data;
using Oregon.Backend.Games;
using Oregon.Backend.Realtime;
using Oregon.Backend.Settings;
using Temporalio.Extensions.Hosting;

namespace Oregon.Backend.Infrastructure;

/// <summary>Registers every backend service. Kept out of Program.cs so the composition is readable and testable.</summary>
public static class BackendServiceRegistration
{
    /// <summary>Adds all backend services.</summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The same builder.</returns>
    public static WebApplicationBuilder AddOregonBackend(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        IServiceCollection services = builder.Services;
        ConfigurationManager configuration = builder.Configuration;
        bool isDevelopment = builder.Environment.IsDevelopment();

        services.AddOptions<AuthSettings>().Bind(configuration.GetSection(AuthSettings.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<TemporalSettings>().Bind(configuration.GetSection(TemporalSettings.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<DatabaseSettings>().Bind(configuration.GetSection(DatabaseSettings.SectionName));
        services.AddOptions<ProxySettings>().Bind(configuration.GetSection(ProxySettings.SectionName));

        AddData(services);
        AddAuthentication(services, isDevelopment);
        AddRateLimiting(services);
        AddTemporal(services, configuration);

        services.AddProblemDetails();
        services.AddExceptionHandler<GameExceptionHandler>();
        services.AddValidation();
        services.AddSignalR();
        services.AddOpenApi();
        services.AddHttpContextAccessor();
        services.AddHealthChecks()
            .AddDbContextCheck<OregonDbContext>("database", tags: ["ready"])
            .AddCheck<TemporalHealthCheck>("temporal", tags: ["ready"]);
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            // TLS terminates at the Container Apps ingress, which forwards plain HTTP plus X-Forwarded-*. The
            // ingress's own addresses are dynamic inside the VNet, so known proxies cannot be pinned; the ingress
            // only accepts traffic from Cloudflare's ranges, which is what makes trusting these headers safe.
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ClientIpResolver>();
        services.AddSingleton<IPasswordVerifier, PasswordVerifier>();
        services.AddScoped<ISaveSlotService, SaveSlotService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<CommandContext>();
        return builder;
    }

    private static void AddData(IServiceCollection services)
    {
        // Resolved lazily from IConfiguration so tests (and Key Vault-backed env vars) can supply it late.
        services.AddDbContext<OregonDbContext>((provider, options) =>
        {
            string connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString("Oregon") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("ConnectionStrings:Oregon is not configured.");
            }

            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
        });

        // Must be the first hosted service: the projection worker and Kestrel start only after migrations.
        services.AddHostedService<DatabaseMigrator>();

        // Keys in Postgres keep session cookies valid across container restarts and new revisions.
        services.AddDataProtection().SetApplicationName("oregon").PersistKeysToDbContext<OregonDbContext>();
    }

    private static void AddAuthentication(IServiceCollection services, bool isDevelopment)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
        {
            // "__Host-" pins the cookie to this exact host over HTTPS; local HTTP development cannot use it.
            options.Cookie.Name = isDevelopment ? "oregon_session" : "__Host-oregon_session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.SlidingExpiration = true;

            // This is an API: answer 401/403 instead of redirecting to a login page.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthSettings>>((options, auth) => options.ExpireTimeSpan = TimeSpan.FromHours(auth.Value.SessionHours));
        services.AddAuthorization();
    }

    private static void AddRateLimiting(IServiceCollection services) => services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy(RateLimitPolicies.Login, context => RateLimitPartition.GetFixedWindowLimiter(
            ClientIp(context),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        options.AddPolicy(RateLimitPolicies.Commands, context => RateLimitPartition.GetTokenBucketLimiter(
            ClientIp(context),
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 60,
                TokensPerPeriod = 60,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
        options.AddPolicy(RateLimitPolicies.StorySeeds, context => RateLimitPartition.GetFixedWindowLimiter(
            ClientIp(context),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    });

    private static void AddTemporal(IServiceCollection services, ConfigurationManager configuration)
    {
        TemporalSettings temporal = configuration.GetSection(TemporalSettings.SectionName).Get<TemporalSettings>() ?? new TemporalSettings();
        services.AddTemporalClient(options => TemporalConnection.Configure(options, temporal));
        services.AddSingleton<IGameEngine, TemporalGameEngine>();
        if (temporal.RunWebWorker)
        {
            services.AddHostedTemporalWorker(temporal.WebTaskQueue).AddScopedActivities<GameProjectionActivities>();
        }
    }

    private static string ClientIp(HttpContext context) =>
        context.RequestServices.GetRequiredService<ClientIpResolver>().Resolve(context);
}
