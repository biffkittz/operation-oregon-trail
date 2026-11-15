using Oregon.Worker.Hosting;

// Composition root for the Temporal game worker. All registrations live in AddOregonWorker so the
// integration tests can build the same service graph.
HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
builder.Services.AddOregonWorker(builder.Configuration, builder.Environment);

using IHost host = builder.Build();
await host.RunAsync();
