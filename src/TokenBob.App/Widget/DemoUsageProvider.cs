using TokenBob.Core.Usage;

namespace TokenBob.App.Widget;

/// <summary>
/// Developer tool for <c>TokenBob.exe --demo-alerts</c>: walks the 5-hour limit through
/// 70 → 82 → 91 → 96 % and then a reset, so every alert can be seen in a few seconds.
/// </summary>
internal sealed class DemoUsageProvider : IUsageProvider
{
    private static readonly double[] Steps = [70, 82, 91, 96];
    private int _step;

    public bool Finished => _step > Steps.Length;

    public Task<UsageResult> FetchAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.Now;
        var window = now.Date.AddHours(now.Hour + 2);
        var weekly = new LimitUsage(64, now.AddDays(3));
        var five = _step < Steps.Length
            ? new LimitUsage(Steps[_step], window)
            : new LimitUsage(4, window.AddHours(5));   // the window reset
        _step = Math.Min(_step + 1, Steps.Length + 1);
        return Task.FromResult(UsageResult.Ok(new UsageSnapshot(five, weekly, now)));
    }
}
