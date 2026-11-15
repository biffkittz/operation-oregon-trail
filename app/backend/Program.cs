using Oregon.Backend.Data;
using Oregon.Backend.Infrastructure;

// Composition root. Service registration lives in BackendServiceRegistration, the pipeline in BackendPipeline.
// `dotnet Oregon.Backend.dll migrate` runs the database setup job (migrations + least-privilege role) and exits.
if (args.Length > 0 && args[0] == DatabaseSetup.Command)
{
    return await DatabaseSetup.RunAsync(args[1..]);
}

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddOregonBackend();

WebApplication app = builder.Build();
app.UseOregonBackend();
await app.RunAsync();
return 0;

/// <summary>Entry point. Partial and public so integration tests can use <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
