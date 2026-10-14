namespace TokenBob.Core.Usage;

/// <summary>One plan limit. <paramref name="Utilization"/> is 0–100 (can exceed 100).</summary>
public sealed record LimitUsage(double Utilization, DateTimeOffset? ResetsAt);

public sealed record UsageSnapshot(LimitUsage FiveHour, LimitUsage Weekly, DateTimeOffset FetchedAt);

public enum UsageError
{
    NotSignedIn,
    Offline,
    RateLimited,
    Unreadable,
}

public sealed record UsageResult(UsageSnapshot? Snapshot, UsageError? Error)
{
    public static UsageResult Ok(UsageSnapshot snapshot) => new(snapshot, null);
    public static UsageResult Fail(UsageError error) => new(null, error);
}
