using Oregon.Backend.Infrastructure;

// Composition root. Service registration lives in BackendServiceRegistration, the pipeline in BackendPipeline.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.AddOregonBackend();

WebApplication app = builder.Build();
app.UseOregonBackend();
await app.RunAsync();

/// <summary>Entry point. Partial and public so integration tests can use <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
