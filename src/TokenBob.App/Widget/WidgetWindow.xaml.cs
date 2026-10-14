using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TokenBob.App.Widget.Styles;
using TokenBob.Core.Config;
using TokenBob.Core.Physics;
using TokenBob.Core.Screens;
using static TokenBob.App.Platform.NativeMethods;

namespace TokenBob.App.Widget;

/// <summary>
/// The transparent, always-on-top window the ornament hangs in. It is only as big as
/// the swing area, sits on one monitor, and never takes keyboard focus.
/// </summary>
public partial class WidgetWindow : Window
{
    private const double OrnamentRoom = 160;
    private static readonly TimeSpan HoverDelay = TimeSpan.FromMilliseconds(400);

    private readonly WidgetSettings _settings;
    private readonly RopeSim _sim;
    private readonly Stopwatch _clock = new();
    private PlacementResult _placement;
    private IntPtr _hwnd;
    private bool _rendering;
    private TimeSpan _lastFrame;
    private readonly DispatcherTimer _hoverTimer;

    public WidgetWindow(WidgetSettings settings, PlacementResult placement)
    {
        InitializeComponent();
        _settings = settings;
        _placement = placement;
        _sim = new RopeSim(Vec2.Zero, placement.RopeLength, settings.SwingStrength);

        Ring.Scale = settings.OrnamentScale;
        Rope.RopeStyle = settings.Rope;

        SourceInitialized += OnSourceInitialized;
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(ApplyPlacement);

        _hoverTimer = new DispatcherTimer(HoverDelay, DispatcherPriority.Normal, OnHoverTimer, Dispatcher) { IsEnabled = false };
        Ring.MouseEnter += (_, _) => _hoverTimer.Start();
        Ring.MouseLeave += (_, _) => HideHover();
        Ring.PreviewMouseLeftButtonDown += (_, _) => HideHover();
    }

    internal RingOrnament Ornament => Ring;

    internal HoverCard Card => HoverContent;

    internal Canvas Surface => Root;

    internal RopeSim Sim => _sim;

    public PlacementResult Placement => _placement;

    /// <summary>The window's current bounds in device pixels.</summary>
    public ScreenRect WindowRectDevice { get; private set; }

    /// <summary>Where the middle of the badge is, in window coordinates (for wind and hover).</summary>
    internal Vec2 OrnamentCenter => _sim.OrnamentPosition + new Vec2(0, Ring.CenterOffset);

    public void MoveTo(PlacementResult placement)
    {
        _placement = placement;
        ApplyPlacement();
    }

    /// <summary>Starts the render loop if the rope is moving (or was just nudged).</summary>
    public void Wake()
    {
        if (_rendering) return;
        _rendering = true;
        _clock.Restart();
        _lastFrame = TimeSpan.Zero;
        CompositionTarget.Rendering += OnFrame;
    }

    /// <summary>Keeps a dragged hang point inside the window so the ornament is never clipped.</summary>
    internal Vec2 ClampToWindow(Vec2 hangTarget)
    {
        var half = Ring.DesiredSize.Width / 2;
        var x = Math.Clamp(hangTarget.X, half, Math.Max(half, Root.Width - half));
        var y = Math.Clamp(hangTarget.Y, 0, Math.Max(0, Root.Height - Ring.DesiredSize.Height));
        return new Vec2(x, y);
    }

    private void OnHoverTimer(object? sender, EventArgs e)
    {
        _hoverTimer.Stop();
        if (Ring.IsMouseOver && Mouse.LeftButton == MouseButtonState.Released) Hover.IsOpen = true;
    }

    private void HideHover()
    {
        _hoverTimer.Stop();
        Hover.IsOpen = false;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        var style = GetWindowLongPtr(_hwnd, GWL_EXSTYLE).ToInt64();
        SetWindowLongPtr(_hwnd, GWL_EXSTYLE, new IntPtr(style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE));
        ApplyPlacement();
        Wake();
    }

    private void ApplyPlacement()
    {
        if (_hwnd == IntPtr.Zero) return;

        var rope = _placement.RopeLength;
        var room = OrnamentRoom * _settings.OrnamentScale;
        var rect = Core.Screens.Placement.WindowRect(_placement, 2.5 * rope + room, 1.4 * rope + room);
        SetWindowPos(_hwnd, HWND_TOPMOST, (int)Math.Round(rect.Left), (int)Math.Round(rect.Top),
            (int)Math.Round(rect.Width), (int)Math.Round(rect.Height), SWP_NOACTIVATE);
        WindowRectDevice = rect;

        var scale = _placement.Monitor.Scale;
        Root.Width = rect.Width / scale;
        Root.Height = rect.Height / scale;
        Rope.Width = Root.Width;
        Rope.Height = Root.Height;

        _sim.Anchor = new Vec2((_placement.AnchorXDevice - rect.Left) / scale, 0);
        _sim.RestLength = rope;
        Wake();
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        _sim.Advance((now - _lastFrame).TotalSeconds);
        _lastFrame = now;
        UpdateVisuals();

        if (_sim.IsAtRest)
        {
            CompositionTarget.Rendering -= OnFrame;
            _rendering = false;
        }
    }

    private void UpdateVisuals()
    {
        Rope.Update(_sim.Points);

        var hang = _sim.OrnamentPosition;
        var width = Ring.DesiredSize.Width > 0 ? Ring.DesiredSize.Width : Ring.ActualWidth;
        Canvas.SetLeft(Ring, hang.X - width / 2);
        Canvas.SetTop(Ring, hang.Y);
        Ring.RenderTransform = new RotateTransform(_sim.OrnamentAngle * 180 / Math.PI, width / 2, 0);
    }
}
