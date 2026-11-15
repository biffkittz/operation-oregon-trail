namespace Oregon.Backend.Infrastructure;

/// <summary>Named rate-limiting policies.</summary>
public static class RateLimitPolicies
{
    /// <summary>Brute-force protection for the single password: 5 attempts per minute per client IP.</summary>
    public const string Login = "login";

    /// <summary>Player commands: generous, but stops runaway scripts from flooding Temporal.</summary>
    public const string Commands = "commands";

    /// <summary>Story seeds call the LLM directly, so they are throttled hardest (cost control).</summary>
    public const string StorySeeds = "story-seeds";
}
