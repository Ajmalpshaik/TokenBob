using TokenBob.Core.Logging;

namespace TokenBob.Core.Usage;

public sealed record UsageState(UsageSnapshot? LastGood, UsageError? Status, bool IsLoading);

/// <summary>
/// Fetches usage now and then on a timer. Call <see cref="Start"/> on the thread
/// that should receive <see cref="Updated"/> (the UI thread): continuations resume
/// on its synchronization context.
/// </summary>
public sealed class UsagePoller : IDisposable
{
    public static readonly TimeSpan ManualCooldown = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(30);

    /// <summary>After going offline (e.g. waking from sleep before Wi-Fi is back), retry soon: 15 s, 30 s, 60 s…</summary>
    public static readonly TimeSpan FirstOfflineRetry = TimeSpan.FromSeconds(15);

    private static readonly TimeSpan OverdueGrace = TimeSpan.FromMinutes(1);

    private readonly IUsageProvider _provider;
    private readonly TimeProvider _time;
    private readonly ILog _log;
    private readonly CancellationTokenSource _stop = new();
    private readonly object _gate = new();
    private TaskCompletionSource _wake = NewSignal();
    private TimeSpan _interval;
    private TimeSpan _currentDelay;
    private bool _backingOff;
    private int _offlineStreak;
    private bool _forcePending;
    private bool _refreshPending;
    private DateTimeOffset? _lastManualRefresh;
    private int _waitsStarted;
    private bool _started;

    public UsagePoller(IUsageProvider provider, TimeProvider time, TimeSpan interval, ILog? log = null)
    {
        _provider = provider;
        _time = time;
        _log = log ?? NullLog.Instance;
        _interval = interval;
        _currentDelay = interval;
        State = new UsageState(null, null, false);
    }

    public event Action<UsageState>? Updated;

    public UsageState State { get; private set; }

    /// <summary>When the latest fetch started (to spot a timer that missed its slot, e.g. in sleep).</summary>
    public DateTimeOffset? LastAttemptAt { get; private set; }

    /// <summary>
    /// Minutes between fetches. Changing it applies at once: the wait in progress is replaced
    /// by one of the new length, measured from the last fetch.
    /// </summary>
    public TimeSpan Interval
    {
        get => _interval;
        set
        {
            lock (_gate)
            {
                _interval = value;
                if (!_backingOff && _offlineStreak == 0) _currentDelay = value;
                _wake.TrySetResult();   // the wait in progress ends and starts over with the new length
            }
        }
    }

    /// <summary>The wait before the next scheduled fetch.</summary>
    public TimeSpan CurrentDelay => _currentDelay;

    /// <summary>Number of timer waits begun so far (lets tests advance fake time safely).</summary>
    internal int WaitsStarted => Volatile.Read(ref _waitsStarted);

    /// <summary>True when the next fetch is more than a minute late (its timer didn't fire).</summary>
    public static bool IsOverdue(DateTimeOffset? lastAttempt, TimeSpan delay, DateTimeOffset now) =>
        lastAttempt is { } last && now - last > delay + OverdueGrace;

    public void Start()
    {
        if (_started) return;
        _started = true;
        LoopAsync().ContinueWith(t => _log.Error("Usage polling stopped", t.Exception),
            CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    /// <summary>
    /// Fetch now. Returns false, without fetching, within 30 s of the previous call —
    /// unless <paramref name="ignoreCooldown"/> is set (after signing in or out, or waking up).
    /// A forced refresh that arrives during a fetch causes another fetch right after it.
    /// </summary>
    public bool RefreshNow(bool ignoreCooldown = false)
    {
        var now = _time.GetUtcNow();
        lock (_gate)
        {
            if (!ignoreCooldown && _lastManualRefresh is { } last && now - last < ManualCooldown) return false;
            _lastManualRefresh = now;
            if (ignoreCooldown) _forcePending = true;
            _refreshPending = true;
            _wake.TrySetResult();
        }
        return true;
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    private async Task LoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            await FetchOnceAsync();
            if (_stop.IsCancellationRequested) return;   // disposed during the fetch: its token is gone

            TaskCompletionSource signal;
            bool fetchAgain;
            lock (_gate)
            {
                // A plain refresh asked for during that fetch is satisfied by it; a forced one
                // (sign-in or sign-out finished meanwhile) needs a fresh fetch. A new interval
                // is simply used by the wait that starts now.
                fetchAgain = _wake.Task.IsCompleted && _forcePending;
                if (_wake.Task.IsCompleted) _wake = NewSignal();
                _forcePending = false;
                _refreshPending = false;
                signal = _wake;
            }
            if (fetchAgain) continue;

            var fromLastFetch = false;
            while (await WaitAsync(signal, fromLastFetch) == WaitOutcome.NewInterval)
            {
                // The interval changed mid-wait: wait again with the new one, counted from the last fetch.
                lock (_gate) signal = _wake;
                fromLastFetch = true;
            }
            if (_stop.IsCancellationRequested) return;
        }
    }

    private enum WaitOutcome { Timer, Refresh, NewInterval }

    /// <summary>Waits for the timer, a manual refresh, or a change of interval.</summary>
    private async Task<WaitOutcome> WaitAsync(TaskCompletionSource signal, bool fromLastFetch)
    {
        var wait = _currentDelay;
        if (fromLastFetch && LastAttemptAt is { } last)
        {
            var elapsed = _time.GetUtcNow() - last;
            wait = elapsed >= wait ? TimeSpan.Zero : wait - elapsed;
        }

        using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        var delay = Task.Delay(wait, _time, delayCts.Token);
        Interlocked.Increment(ref _waitsStarted);

        var finished = await Task.WhenAny(delay, signal.Task);
        delayCts.Cancel();
        if (finished != signal.Task) return WaitOutcome.Timer;

        lock (_gate)
        {
            if (ReferenceEquals(_wake, signal)) _wake = NewSignal();
            var refresh = _refreshPending;
            _refreshPending = false;
            _forcePending = false;
            return refresh ? WaitOutcome.Refresh : WaitOutcome.NewInterval;
        }
    }

    private async Task FetchOnceAsync()
    {
        LastAttemptAt = _time.GetUtcNow();
        Publish(State with { IsLoading = true });

        UsageResult result;
        try
        {
            result = await _provider.FetchAsync(_stop.Token);
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _log.Error("Usage fetch failed unexpectedly", ex);
            result = UsageResult.Fail(UsageError.Unreadable);
        }

        ScheduleNext(result.Error);
        Publish(result.Snapshot is { } snapshot
            ? new UsageState(snapshot, null, false)
            : new UsageState(State.LastGood, result.Error, false));
    }

    private void ScheduleNext(UsageError? error)
    {
        switch (error)
        {
            case UsageError.RateLimited:
                _backingOff = true;
                _offlineStreak = 0;
                var doubled = _currentDelay * 2;
                _currentDelay = doubled > MaxBackoff ? MaxBackoff : doubled;
                break;
            case UsageError.Offline:
                _backingOff = false;
                _offlineStreak++;
                var retry = FirstOfflineRetry * Math.Pow(2, Math.Min(_offlineStreak - 1, 10));
                _currentDelay = retry < _interval ? retry : _interval;
                break;
            default:
                _backingOff = false;
                _offlineStreak = 0;
                _currentDelay = _interval;
                break;
        }
    }

    private void Publish(UsageState state)
    {
        State = state;
        try
        {
            Updated?.Invoke(state);
        }
        catch (Exception ex)
        {
            // A failing listener (say, a locked file while saving) must never stop the polling.
            _log.Error("A usage update handler failed", ex);
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
