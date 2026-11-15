namespace Oregon.Backend.Tests.Support;

/// <summary>API tests share one database, so they run sequentially in this collection.</summary>
[CollectionDefinition(nameof(ApiCollection), DisableParallelization = true)]
public sealed class ApiCollection;
