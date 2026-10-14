using System.Globalization;
using TokenBob.Core.Usage;

namespace TokenBob.Core.Alerts;

public enum ColorLevel { Green, Amber, Red }

public enum LimitKind { FiveHour, Weekly }

public abstract record AlertEvent;

/// <summary>A limit reached a notification threshold (80 or 95) for the first time in its window.</summary>
public sealed record ThresholdCrossed(LimitKind Limit, int Threshold) : AlertEvent;

public sealed record Shiver : AlertEvent;

public sealed record ResetBounce : AlertEvent;

/// <summary>Thresholds already announced, keyed by limit window. Persisted so restarts don't repeat them.</summary>
public sealed class AlertMemory
{
    public HashSet<string> Fired { get; set; } = new();
}

public static class AlertRules
{
    private static readonly int[] Thresholds = [80, 95];
    private const double ShiverAt = 90;

    public static ColorLevel Level(double utilization) => utilization switch
    {
        < 60 => ColorLevel.Green,
        <= 85 => ColorLevel.Amber,
        _ => ColorLevel.Red,
    };

    /// <summary>Works out what to announce for a new snapshot. Only <paramref name="memory"/> is changed.</summary>
    public static IReadOnlyList<AlertEvent> Evaluate(UsageSnapshot? previous, UsageSnapshot current, AlertMemory memory)
    {
        var events = new List<AlertEvent>();
        AddCrossing(events, memory, LimitKind.FiveHour, current.FiveHour);
        AddCrossing(events, memory, LimitKind.Weekly, current.Weekly);

        if (current.FiveHour.Utilization >= ShiverAt) events.Add(new Shiver());

        // The window reset: there was a reset time, and now there's a later one or none at all
        // (an idle window reports no reset time), and usage went down.
        if (previous?.FiveHour.ResetsAt is { } before
            && (current.FiveHour.ResetsAt is not { } after || after > before)
            && current.FiveHour.Utilization < previous.FiveHour.Utilization)
        {
            events.Add(new ResetBounce());
        }

        return events;
    }

    private static void AddCrossing(List<AlertEvent> events, AlertMemory memory, LimitKind limit, LimitUsage usage)
    {
        // Only the highest new threshold is announced; lower ones are marked as done with it.
        int? highest = null;
        foreach (var threshold in Thresholds)
        {
            if (usage.Utilization < threshold) continue;
            if (memory.Fired.Add(Key(limit, threshold, usage.ResetsAt))) highest = threshold;
        }
        if (highest is { } t) events.Add(new ThresholdCrossed(limit, t));
    }

    private static string Key(LimitKind limit, int threshold, DateTimeOffset? resetsAt) =>
        $"{limit}:{threshold}@{resetsAt?.ToString("O", CultureInfo.InvariantCulture) ?? "none"}";
}
