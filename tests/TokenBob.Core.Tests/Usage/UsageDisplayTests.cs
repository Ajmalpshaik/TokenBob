using System.Globalization;
using TokenBob.Core.Alerts;
using TokenBob.Core.Usage;

namespace TokenBob.Core.Tests.Usage;

public class UsageDisplayTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    private static UsageSnapshot Snap(double five = 42, double week = 18, DateTimeOffset? fiveReset = null,
        DateTimeOffset? weekReset = null, TimeSpan? age = null, bool nullResets = false) =>
        new(new LimitUsage(five, nullResets ? null : fiveReset ?? new DateTimeOffset(2026, 10, 14, 12, 10, 0, TimeSpan.Zero)),
            new LimitUsage(week, nullResets ? null : weekReset ?? new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero)),
            Now - (age ?? TimeSpan.FromMinutes(3)));

    private static UsageDisplayModel Show(UsageSnapshot? lastGood, UsageError? status = null, bool loading = false) =>
        UsageDisplay.From(new UsageState(lastGood, status, loading), Now, TimeZoneInfo.Utc, EnUs);

    [Fact]
    public void Ok_state()
    {
        var m = Show(Snap());

        Assert.Equal("42%", m.CenterText);
        Assert.False(m.IsGrey);
        Assert.False(m.IsLoading);
        Assert.Equal("resets 2h 10m", m.FiveHour!.ResetShort);
        Assert.Equal("resets 12:10 PM", m.FiveHour.ResetClock);
        Assert.Equal("resets Fri", m.Weekly!.ResetShort);
        Assert.Equal("resets Fri 9:00 AM", m.Weekly.ResetClock);
        Assert.Equal(ColorLevel.Green, m.FiveHour.Level);
        Assert.Equal(["5-hour: 42% · resets 12:10 PM", "Weekly: 18% · resets Fri 9:00 AM", "Updated 3 min ago"], m.HoverLines);
        Assert.Equal("5h 42% · Week 18%", m.TrayTooltip);
    }

    [Fact]
    public void Under_one_minute_says_updated_just_now()
    {
        Assert.Equal("Updated just now", Show(Snap(age: TimeSpan.FromSeconds(20))).HoverLines[^1]);
    }

    [Fact]
    public void Under_one_hour_reset_omits_hours()
    {
        var m = Show(Snap(fiveReset: Now.AddMinutes(45)));
        Assert.Equal("resets 45m", m.FiveHour!.ResetShort);
    }

    [Fact]
    public void Null_reset_says_not_started_yet()
    {
        var m = Show(Snap(five: 0, nullResets: true));
        Assert.Equal("Not started yet", m.FiveHour!.ResetShort);
        Assert.Equal("Not started yet", m.FiveHour.ResetClock);
        Assert.Equal("5-hour: 0% · Not started yet", m.HoverLines[0]);
    }

    [Fact]
    public void Not_signed_in()
    {
        var m = Show(null, UsageError.NotSignedIn);
        Assert.Equal("?", m.CenterText);
        Assert.True(m.IsGrey);
        Assert.Null(m.FiveHour);
        Assert.Equal(["Sign in to Claude to see usage"], m.HoverLines);
        Assert.Equal("TokenBob — Sign in to Claude to see usage", m.TrayTooltip);
    }

    [Theory]
    [InlineData(UsageError.Offline)]
    [InlineData(UsageError.RateLimited)]
    public void Offline_with_last_good(UsageError error)
    {
        var m = Show(Snap(age: TimeSpan.FromMinutes(12)), error);
        Assert.Equal("42%", m.CenterText);
        Assert.True(m.IsGrey);
        Assert.Equal("Offline — updated 12 min ago", m.HoverLines[^1]);
        Assert.Equal(3, m.HoverLines.Count);
    }

    [Fact]
    public void Offline_without_data()
    {
        var m = Show(null, UsageError.Offline);
        Assert.Equal("?", m.CenterText);
        Assert.Equal(["Offline — can't reach claude.ai"], m.HoverLines);
    }

    [Fact]
    public void Unreadable_with_last_good()
    {
        var m = Show(Snap(), UsageError.Unreadable);
        Assert.Equal("42%", m.CenterText);
        Assert.True(m.IsGrey);
        Assert.Equal("Can't read usage right now", m.HoverLines[^1]);
    }

    [Fact]
    public void Unreadable_without()
    {
        var m = Show(null, UsageError.Unreadable);
        Assert.Equal("?", m.CenterText);
        Assert.Equal(["Can't read usage right now"], m.HoverLines);
    }

    [Fact]
    public void Loading_without_data()
    {
        var m = Show(null, loading: true);
        Assert.True(m.IsLoading);
        Assert.Equal("…", m.CenterText);
        Assert.Equal(["Loading usage…"], m.HoverLines);
    }

    [Fact]
    public void Refreshing_with_data_keeps_numbers_without_pulse()
    {
        var m = Show(Snap(), loading: true);
        Assert.False(m.IsLoading);
        Assert.Equal("42%", m.CenterText);
    }

    [Fact]
    public void Notification_text()
    {
        var s = Snap(five: 81, week: 96);
        Assert.Equal(("TokenBob", "5-hour limit at 80% — resets 12:10 PM"),
            UsageDisplay.NotificationText(new ThresholdCrossed(LimitKind.FiveHour, 80), s, Now, TimeZoneInfo.Utc, EnUs));
        Assert.Equal(("TokenBob", "Weekly limit at 95% — resets Fri 9:00 AM"),
            UsageDisplay.NotificationText(new ThresholdCrossed(LimitKind.Weekly, 95), s, Now, TimeZoneInfo.Utc, EnUs));
    }

    [Fact]
    public void Over_100_percent_clamps_ring_but_shows_real_number()
    {
        var m = Show(Snap(five: 104));
        Assert.Equal(100, m.FiveHour!.RingPercent);
        Assert.Equal("104%", m.FiveHour.PercentText);
        Assert.Equal(ColorLevel.Red, m.FiveHour.Level);
    }
}
