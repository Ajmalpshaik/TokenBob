using System.Windows.Input;
using System.Windows.Threading;
using TokenBob.App.Widget;
using TokenBob.Core.Config;
using TokenBob.Core.Input;
using TokenBob.Core.Physics;
using TokenBob.Core.Screens;
using static TokenBob.App.Platform.NativeMethods;

namespace TokenBob.App.Input;

/// <summary>
/// Feeds mouse input on the ornament to <see cref="GestureRecognizer"/> and carries out
/// the result: fling, Shift + drag placement, Ctrl + click, double-click, and wind.
/// </summary>
internal sealed class GestureController
{
    private const double PullRefreshStretch = 0.25;

    private readonly WidgetWindow _window;
    private readonly WidgetSettings _settings;
    private readonly Func<IReadOnlyList<MonitorInfo>> _monitors;
    private readonly GestureRecognizer _recognizer = new(TimeSpan.FromMilliseconds(GetDoubleClickTime()));
    private readonly WindDetector _wind = new();
    private readonly DispatcherTimer _windTimer;
    private Vec2 _grabOffset;
    private bool _busy;

    public GestureController(WidgetWindow window, WidgetSettings settings, Func<IReadOnlyList<MonitorInfo>> monitors)
    {
        _window = window;
        _settings = settings;
        _monitors = monitors;

        var ornament = window.OrnamentHost;
        ornament.MouseLeftButtonDown += OnDown;
        ornament.MouseMove += OnMove;
        ornament.MouseLeftButtonUp += OnUp;
        ornament.LostMouseCapture += (_, _) => _busy = false;

        _windTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(1000.0 / 30), DispatcherPriority.Input, OnWindTick, window.Dispatcher);
        _windTimer.Start();
    }

    public event Action? SettingsRequested;
    public event Action? UsagePageRequested;
    public event Action? PullRefreshRequested;
    public event Action<PlacementResult>? PlacementChanged;

    private static TimeSpan Now => TimeSpan.FromMilliseconds(Environment.TickCount64);

    private static Modifiers CurrentModifiers()
    {
        var mods = Modifiers.None;
        if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0) mods |= Modifiers.Ctrl;
        if ((GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0) mods |= Modifiers.Shift;
        return mods;
    }

    private Vec2 Position(MouseEventArgs e)
    {
        var p = e.GetPosition(_window.Surface);
        return new Vec2(p.X, p.Y);
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        var p = Position(e);
        _grabOffset = p - _window.Sim.OrnamentPosition;
        Handle(_recognizer.Down(p, CurrentModifiers(), Now));
        if (!_busy) _window.OrnamentHost.CaptureMouse();
        e.Handled = true;
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_window.OrnamentHost.IsMouseCaptured) return;
        Handle(_recognizer.Move(Position(e), Now));
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        var events = _recognizer.Up(Position(e), Now);
        if (_window.OrnamentHost.IsMouseCaptured) _window.OrnamentHost.ReleaseMouseCapture();
        Handle(events);
    }

    private void Handle(IReadOnlyList<GestureEvent> events)
    {
        foreach (var g in events)
        {
            switch (g.Kind)
            {
                case GestureEventKind.OpenSettings:
                    SettingsRequested?.Invoke();
                    break;
                case GestureEventKind.OpenUsagePage:
                    UsagePageRequested?.Invoke();
                    break;
                case GestureEventKind.DragStart:
                case GestureEventKind.DragMove:
                    _busy = true;
                    _window.Sim.Grab(_window.ClampToWindow(g.Position - _grabOffset));
                    _window.Wake();
                    break;
                case GestureEventKind.DragEnd:
                    _busy = false;
                    var pulledHard = _window.Sim.StretchRatio > PullRefreshStretch;
                    _window.Sim.Release();
                    _window.Wake();
                    if (pulledHard) PullRefreshRequested?.Invoke();
                    break;
                case GestureEventKind.EditStart:
                    _busy = true;
                    _window.Sim.Freeze(true);
                    EditMove();
                    break;
                case GestureEventKind.EditMove:
                    EditMove();
                    break;
                case GestureEventKind.EditEnd:
                    _busy = false;
                    _window.Sim.Freeze(false);
                    _window.Sim.ApplyImpulse(new Vec2(0, 200));
                    _window.Wake();
                    PlacementChanged?.Invoke(_window.Placement);
                    break;
            }
        }
    }

    private void EditMove()
    {
        if (!GetCursorPos(out var cursor)) return;
        var placement = Placement.Edit(_monitors(), _window.Placement, new Vec2(cursor.X, cursor.Y), _grabOffset.Y);
        _window.MoveTo(placement);
    }

    private void OnWindTick(object? sender, EventArgs e)
    {
        if (!_settings.Wind || _busy || !_window.IsVisible || !GetCursorPos(out var cursor)) return;

        var rect = _window.WindowRectDevice;
        var scale = _window.Placement.Monitor.Scale;
        var local = new Vec2((cursor.X - rect.Left) / scale, (cursor.Y - rect.Top) / scale);
        if (_wind.Sample(local, _window.OrnamentCenter, Now) is { } gust)
        {
            _window.Sim.ApplyImpulse(gust);
            _window.Wake();
        }
    }
}
