using System.Collections.Concurrent;
using Microsoft.Extensions.Time.Testing;
using TokenBob.Core.Usage;

namespace TokenBob.Core.Tests.Usage;

public class UsagePollerTests
{
    private static readonly TimeSpan Five = TimeSpan.FromMinutes(5);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero));

    private sealed class QueueProvider : IUsageProvider
    {
        private readonly ConcurrentQueue<UsageResult> _queue = new();
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);
        public UsageResult Default { get; set; } = UsageResult.Ok(Snapshot(10));

        public QueueProvider Enqueue(params UsageResult[] results)
        {
            foreach (var r in results) _queue.Enqueue(r);
            return this;
        }

        public Task<UsageResult> FetchAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(_queue.TryDequeue(out var r) ? r : Default);
        }
    }

    private static UsageSnapshot Snapshot(double fiveHour) =>
        new(new LimitUsage(fiveHour, null), new LimitUsage(1, null), DateTimeOffset.UnixEpoch);

    private static void Eventually(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("condition not met");
            Thread.Sleep(5);
        }
    }

    private static void WaitForWait(UsagePoller poller, int count) => Eventually(() => poller.WaitsStarted >= count);

    [Fact]
    public void Start_fetches_immediately_and_reports_loading_then_snapshot()
    {
        var provider = new QueueProvider();
        using var poller = new UsagePoller(provider, _time, Five);
        var states = new ConcurrentQueue<UsageState>();
        poller.Updated += states.Enqueue;

        poller.Start();
        Eventually(() => states.Count >= 2);

        var list = states.ToArray();
        Assert.True(list[0].IsLoading);
        Assert.False(list[1].IsLoading);
        Assert.Equal(10, list[1].LastGood!.FiveHour.Utilization);
        Assert.Null(list[1].Status);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public void Fetches_again_after_interval()
    {
        var provider = new QueueProvider();
        using var poller = new UsagePoller(provider, _time, Five);

        poller.Start();
        WaitForWait(poller, 1);
        _time.Advance(Five);

        Eventually(() => provider.Calls == 2);
    }

    [Fact]
    public void RefreshNow_respects_30_second_cooldown()
    {
        var provider = new QueueProvider();
        using var poller = new UsagePoller(provider, _time, Five);
        poller.Start();
        WaitForWait(poller, 1);

        Assert.True(poller.RefreshNow());
        Eventually(() => provider.Calls == 2);
        WaitForWait(poller, 2);

        _time.Advance(TimeSpan.FromSeconds(10));
        Assert.False(poller.RefreshNow());
        Thread.Sleep(50);
        Assert.Equal(2, provider.Calls);

        _time.Advance(TimeSpan.FromSeconds(21));
        Assert.True(poller.RefreshNow());
        Eventually(() => provider.Calls == 3);
    }

    [Fact]
    public void Error_keeps_last_good_snapshot()
    {
        var provider = new QueueProvider().Enqueue(UsageResult.Ok(Snapshot(42)), UsageResult.Fail(UsageError.Offline));
        using var poller = new UsagePoller(provider, _time, Five);

        poller.Start();
        WaitForWait(poller, 1);
        _time.Advance(Five);
        Eventually(() => provider.Calls == 2 && !poller.State.IsLoading);

        Assert.Equal(42, poller.State.LastGood!.FiveHour.Utilization);
        Assert.Equal(UsageError.Offline, poller.State.Status);
    }

    [Fact]
    public void RateLimited_doubles_delay_up_to_30_minutes_and_success_resets()
    {
        var limited = UsageResult.Fail(UsageError.RateLimited);
        var provider = new QueueProvider().Enqueue(limited, limited, limited, limited, UsageResult.Ok(Snapshot(5)));
        using var poller = new UsagePoller(provider, _time, Five);

        poller.Start();
        var expected = new[] { 10, 20, 30, 30 };
        for (var i = 0; i < expected.Length; i++)
        {
            WaitForWait(poller, i + 1);
            Assert.Equal(TimeSpan.FromMinutes(expected[i]), poller.CurrentDelay);
            _time.Advance(poller.CurrentDelay);
        }

        WaitForWait(poller, 5);
        Assert.Equal(Five, poller.CurrentDelay);
        Assert.Equal(5, provider.Calls);
    }

    [Fact]
    public void Changing_interval_applies_to_next_wait()
    {
        var provider = new QueueProvider();
        using var poller = new UsagePoller(provider, _time, Five);
        poller.Start();
        WaitForWait(poller, 1);

        poller.Interval = TimeSpan.FromMinutes(1);
        _time.Advance(Five);
        Eventually(() => provider.Calls == 2);
        WaitForWait(poller, 2);
        _time.Advance(TimeSpan.FromMinutes(1));

        Eventually(() => provider.Calls == 3);
    }
}
