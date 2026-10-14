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

    private readonly IUsageProvider _provider;
    private readonly TimeProvider _time;
    private readonly CancellationTokenSource _stop = new();
    private readonly object _gate = new();
    private TaskCompletionSource _wake = NewSignal();
    private TimeSpan _interval;
    private TimeSpan _currentDelay;
    private bool _backingOff;
    private DateTimeOffset? _lastManualRefresh;
    private int _waitsStarted;
    private bool _started;

    public UsagePoller(IUsageProvider provider, TimeProvider time, TimeSpan interval)
    {
        _provider = provider;
        _time = time;
        _interval = interval;
        _currentDelay = interval;
        State = new UsageState(null, null, false);
    }

    public event Action<UsageState>? Updated;

    public UsageState State { get; private set; }

    public TimeSpan Interval
    {
        get => _interval;
        set
        {
            _interval = value;
            if (!_backingOff) _currentDelay = value;
        }
    }

    /// <summary>The wait before the next scheduled fetch.</summary>
    public TimeSpan CurrentDelay => _currentDelay;

    /// <summary>Number of timer waits begun so far (lets tests advance fake time safely).</summary>
    internal int WaitsStarted => Volatile.Read(ref _waitsStarted);

    public void Start()
    {
        if (_started) return;
        _started = true;
        _ = LoopAsync();
    }

    /// <summary>
    /// Fetch now. Returns false, without fetching, within 30 s of the previous call —
    /// unless <paramref name="ignoreCooldown"/> is set (after signing in or out).
    /// </summary>
    public bool RefreshNow(bool ignoreCooldown = false)
    {
        var now = _time.GetUtcNow();
        lock (_gate)
        {
            if (!ignoreCooldown && _lastManualRefresh is { } last && now - last < ManualCooldown) return false;
            _lastManualRefresh = now;
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

            TaskCompletionSource signal;
            lock (_gate)
            {
                // A refresh requested while that fetch was running is satisfied by it.
                if (_wake.Task.IsCompleted) _wake = NewSignal();
                signal = _wake;
            }
            if (await WaitAsync(signal)) continue;
            if (_stop.IsCancellationRequested) return;
        }
    }

    /// <summary>Waits for the timer or a manual refresh. Returns true when woken early.</summary>
    private async Task<bool> WaitAsync(TaskCompletionSource signal)
    {
        using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        var delay = Task.Delay(_currentDelay, _time, delayCts.Token);
        Interlocked.Increment(ref _waitsStarted);

        var finished = await Task.WhenAny(delay, signal.Task);
        delayCts.Cancel();
        if (finished == signal.Task)
        {
            lock (_gate)
            {
                if (ReferenceEquals(_wake, signal)) _wake = NewSignal();
            }
            return true;
        }
        return false;
    }

    private async Task FetchOnceAsync()
    {
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
        catch (Exception)
        {
            result = UsageResult.Fail(UsageError.Unreadable);
        }

        if (result.Error == UsageError.RateLimited)
        {
            _backingOff = true;
            var doubled = _currentDelay * 2;
            _currentDelay = doubled > MaxBackoff ? MaxBackoff : doubled;
        }
        else
        {
            _backingOff = false;
            _currentDelay = _interval;
        }

        Publish(result.Snapshot is { } snapshot
            ? new UsageState(snapshot, null, false)
            : new UsageState(State.LastGood, result.Error, false));
    }

    private void Publish(UsageState state)
    {
        State = state;
        Updated?.Invoke(state);
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
