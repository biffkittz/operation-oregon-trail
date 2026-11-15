using Oregon.Worker.Game;
using Temporalio.Workflows;

namespace Oregon.Worker.Workflows;

/// <summary>Deterministic clock backed by <see cref="Workflow.UtcNow"/> (the current workflow task's time).</summary>
internal sealed class WorkflowClock : IGameClock
{
    public DateTime UtcNow => Workflow.UtcNow;
}
