using System.Globalization;
using TokenBob.Core.Alerts;

namespace TokenBob.Core.Usage;

public sealed record LimitDisplay(double RingPercent, string PercentText, string ResetShort, string ResetClock, ColorLevel Level);

public sealed record UsageDisplayModel(
    string CenterText,
    bool IsGrey,
    bool IsLoading,
    LimitDisplay? FiveHour,
    LimitDisplay? Weekly,
    IReadOnlyList<string> HoverLines,
    string TrayTooltip);

/// <summary>Every piece of text and color the widget, hover card and tray show for a usage state.</summary>
public static class UsageDisplay
{
    public const string SignInText = "Sign in to Claude to see usage";
    public const string UnreadableText = "Can't read usage right now";
    public const string LoadingText = "Loading usage…";
    public const string OfflineNoDataText = "Offline — can't reach claude.ai";
    private const string NotStarted = "Not started yet";

    public static UsageDisplayModel From(UsageState state, DateTimeOffset now, TimeZoneInfo zone, CultureInfo culture)
    {
        var last = state.LastGood;

        if (state.Status == UsageError.NotSignedIn) return Message("?", SignInText);
        if (last is null)
        {
            return state.Status switch
            {
                UsageError.Offline or UsageError.RateLimited => Message("?", OfflineNoDataText),
                UsageError.Unreadable => Message("?", UnreadableText),
                _ when state.IsLoading => new UsageDisplayModel("…", true, true, null, null, [LoadingText], $"{AppInfo.Name} — {LoadingText}"),
                _ => Message("?", LoadingText),
            };
        }

        var five = Limit(last.FiveHour, now, zone, culture);
        var week = Limit(last.Weekly, now, zone, culture);
        var lines = new List<string>
        {
            $"5-hour: {five.PercentText} · {five.ResetClock}",
            $"Weekly: {week.PercentText} · {week.ResetClock}",
        };

        var minutes = (int)Math.Floor((now - last.FetchedAt).TotalMinutes);
        lines.Add(state.Status switch
        {
            UsageError.Offline or UsageError.RateLimited => $"Offline — updated {minutes} min ago",
            UsageError.Unreadable => UnreadableText,
            _ => minutes < 1 ? "Updated just now" : $"Updated {minutes} min ago",
        });

        return new UsageDisplayModel(five.PercentText, state.Status is not null, false, five, week, lines,
            $"5h {five.PercentText} · Week {week.PercentText}");
    }

    private static UsageDisplayModel Message(string center, string line) =>
        new(center, true, false, null, null, [line], $"{AppInfo.Name} — {line}");

    private static LimitDisplay Limit(LimitUsage usage, DateTimeOffset now, TimeZoneInfo zone, CultureInfo culture)
    {
        var percent = $"{Math.Round(usage.Utilization).ToString(CultureInfo.InvariantCulture)}%";
        var (shortText, clockText) = usage.ResetsAt is { } reset
            ? (ResetShort(reset, now, zone, culture), ResetClock(reset, now, zone, culture))
            : (NotStarted, NotStarted);
        return new LimitDisplay(Math.Clamp(usage.Utilization, 0, 100), percent, shortText, clockText, AlertRules.Level(usage.Utilization));
    }

    private static string ResetShort(DateTimeOffset reset, DateTimeOffset now, TimeZoneInfo zone, CultureInfo culture)
    {
        var left = reset - now;
        if (left <= TimeSpan.Zero) return "resets soon";
        if (left >= TimeSpan.FromHours(24))
            return "resets " + TimeZoneInfo.ConvertTime(reset, zone).ToString("ddd", culture);

        var totalMinutes = (int)Math.Ceiling(left.TotalMinutes);
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        return hours == 0 ? $"resets {minutes}m" : $"resets {hours}h {minutes}m";
    }

    private static string ResetClock(DateTimeOffset reset, DateTimeOffset now, TimeZoneInfo zone, CultureInfo culture)
    {
        var local = TimeZoneInfo.ConvertTime(reset, zone);
        var today = TimeZoneInfo.ConvertTime(now, zone).Date;
        var time = local.ToString("t", culture);
        return local.Date == today ? $"resets {time}" : $"resets {local.ToString("ddd", culture)} {time}";
    }
}
