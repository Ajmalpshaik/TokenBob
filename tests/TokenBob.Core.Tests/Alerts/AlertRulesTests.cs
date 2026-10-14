using TokenBob.Core.Alerts;
using TokenBob.Core.Usage;

namespace TokenBob.Core.Tests.Alerts;

public class AlertRulesTests
{
    private static readonly DateTimeOffset WindowA = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WindowB = new(2026, 10, 14, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WeekReset = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private static UsageSnapshot Snap(double five, DateTimeOffset? fiveReset = null, double week = 10) =>
        new(new LimitUsage(five, fiveReset ?? WindowA), new LimitUsage(week, WeekReset), WindowA);

    private static ThresholdCrossed[] Crossings(IEnumerable<AlertEvent> events) => events.OfType<ThresholdCrossed>().ToArray();

    [Theory]
    [InlineData(59.9, ColorLevel.Green)]
    [InlineData(60, ColorLevel.Amber)]
    [InlineData(85, ColorLevel.Amber)]
    [InlineData(85.1, ColorLevel.Red)]
    public void Level_boundaries(double utilization, ColorLevel expected)
    {
        Assert.Equal(expected, AlertRules.Level(utilization));
    }

    [Fact]
    public void Below_80_no_alert()
    {
        Assert.Empty(AlertRules.Evaluate(null, Snap(79), new AlertMemory()));
    }

    [Fact]
    public void At_80_one_alert_then_no_repeat()
    {
        var memory = new AlertMemory();
        var first = AlertRules.Evaluate(null, Snap(80), memory);
        var again = AlertRules.Evaluate(Snap(80), Snap(81), memory);

        Assert.Equal(new AlertEvent[] { new ThresholdCrossed(LimitKind.FiveHour, 80) }, first);
        Assert.Empty(Crossings(again));
    }

    [Fact]
    public void At_95_fires_once()
    {
        var memory = new AlertMemory();
        var first = AlertRules.Evaluate(null, Snap(95), memory);
        var again = AlertRules.Evaluate(Snap(95), Snap(96), memory);

        Assert.Equal(new[] { new ThresholdCrossed(LimitKind.FiveHour, 95) }, Crossings(first));
        Assert.Empty(Crossings(again));
    }

    [Fact]
    public void Jump_past_both_thresholds_fires_only_95()
    {
        var memory = new AlertMemory();
        var jump = AlertRules.Evaluate(Snap(70), Snap(96), memory);
        var later = AlertRules.Evaluate(Snap(96), Snap(97), memory);

        Assert.Equal(new[] { new ThresholdCrossed(LimitKind.FiveHour, 95) }, Crossings(jump));
        Assert.Empty(Crossings(later));
    }

    [Fact]
    public void Thresholds_rearm_after_window_reset()
    {
        var memory = new AlertMemory();
        AlertRules.Evaluate(null, Snap(85, WindowA), memory);
        var newWindow = AlertRules.Evaluate(Snap(85, WindowA), Snap(85, WindowB), memory);

        Assert.Equal(new[] { new ThresholdCrossed(LimitKind.FiveHour, 80) }, Crossings(newWindow));
    }

    [Fact]
    public void Weekly_thresholds_are_independent()
    {
        var events = AlertRules.Evaluate(null, Snap(10, week: 80), new AlertMemory());
        Assert.Equal(new[] { new ThresholdCrossed(LimitKind.Weekly, 80) }, Crossings(events));
    }

    [Fact]
    public void Reset_detected_gives_bounce()
    {
        var events = AlertRules.Evaluate(Snap(70, WindowA), Snap(5, WindowB), new AlertMemory());
        Assert.Contains(new ResetBounce(), events);
    }

    [Fact]
    public void No_bounce_on_first_evaluation()
    {
        Assert.DoesNotContain(new ResetBounce(), AlertRules.Evaluate(null, Snap(5, WindowB), new AlertMemory()));
    }

    [Fact]
    public void Shiver_at_90_not_at_89_9()
    {
        Assert.Contains(new Shiver(), AlertRules.Evaluate(null, Snap(90), new AlertMemory()));
        Assert.DoesNotContain(new Shiver(), AlertRules.Evaluate(null, Snap(89.9), new AlertMemory()));
    }
}
