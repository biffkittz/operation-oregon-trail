using System.Text.Json;

namespace Oregon.Contracts.Workflows;

/// <summary>
/// Opaque game state carried across Continue-As-New. Only the worker understands <see cref="State"/>;
/// <see cref="SchemaVersion"/> lets a newer worker upgrade state written by an older one.
/// </summary>
/// <param name="SchemaVersion">Version of the worker's internal state schema.</param>
/// <param name="State">Serialized worker state.</param>
public sealed record GameContinuation(int SchemaVersion, JsonElement State);
