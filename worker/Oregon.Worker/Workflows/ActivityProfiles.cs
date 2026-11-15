using Oregon.Worker.Llm;
using Temporalio.Common;
using Temporalio.Workflows;

namespace Oregon.Worker.Workflows;

/// <summary>
/// Timeouts and retry policies per activity family. New instances are returned each time because
/// <see cref="ActivityOptions"/> is mutable.
/// </summary>
internal static class ActivityProfiles
{
    /// <summary>Prose generation: fast model, a few quick retries, then the workflow falls back to canned text.</summary>
    public static ActivityOptions Narrator => new()
    {
        StartToCloseTimeout = TimeSpan.FromSeconds(40),
        ScheduleToCloseTimeout = TimeSpan.FromMinutes(2),
        RetryPolicy = LlmRetry(),
    };

    /// <summary>Judging: slower reasoning model, slightly more patience because the verdict matters.</summary>
    public static ActivityOptions Judge => new()
    {
        StartToCloseTimeout = TimeSpan.FromSeconds(60),
        ScheduleToCloseTimeout = TimeSpan.FromMinutes(3),
        RetryPolicy = LlmRetry(),
    };

    /// <summary>
    /// Read-model projection on the backend's task queue. Short timeouts: if the backend is down the projector
    /// keeps its outbox and tries again later instead of blocking gameplay.
    /// </summary>
    public static ActivityOptions Projection(string taskQueue) => new()
    {
        TaskQueue = taskQueue,
        StartToCloseTimeout = TimeSpan.FromSeconds(15),
        ScheduleToCloseTimeout = TimeSpan.FromMinutes(1),
        RetryPolicy = new RetryPolicy
        {
            InitialInterval = TimeSpan.FromSeconds(1),
            MaximumInterval = TimeSpan.FromSeconds(10),
            MaximumAttempts = 5,
        },
    };

    private static RetryPolicy LlmRetry() => new()
    {
        InitialInterval = TimeSpan.FromSeconds(1),
        BackoffCoefficient = 2,
        MaximumInterval = TimeSpan.FromSeconds(10),
        MaximumAttempts = 4,
        NonRetryableErrorTypes = [LlmErrorTypes.Permanent],
    };
}
