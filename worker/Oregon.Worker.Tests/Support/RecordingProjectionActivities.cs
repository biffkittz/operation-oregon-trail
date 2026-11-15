using System.Collections.Concurrent;
using Oregon.Contracts;
using Oregon.Contracts.Model;
using Oregon.Contracts.Workflows;
using Temporalio.Activities;

namespace Oregon.Worker.Tests.Support;

/// <summary>Stand-in for the backend's projection activity that records what the workflow sends.</summary>
public sealed class RecordingProjectionActivities : IGameProjectionActivities
{
    private readonly ConcurrentQueue<GameProjection> _projections = new();

    /// <summary>Gets or sets a value indicating whether to simulate a backend outage.</summary>
    public bool FailAll { get; set; }

    public GameProjection? Latest => _projections.LastOrDefault();

    public IReadOnlyList<JournalEntryView> AllEntries => [.. _projections.SelectMany(p => p.NewJournalEntries)];

    [Activity(ActivityNames.ProjectGame)]
    public Task ProjectGameAsync(GameProjection projection)
    {
        if (FailAll)
        {
            throw new InvalidOperationException("Simulated backend outage.");
        }

        _projections.Enqueue(projection);
        return Task.CompletedTask;
    }
}
