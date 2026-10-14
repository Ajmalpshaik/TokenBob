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
    private readonly DispatcherTimer _hoverTimer;
    private PlacementResult _placement;
    private IntPtr _hwnd;
    private bool _rendering;
    private TimeSpan _lastFrame;

    public WidgetWindow(WidgetSettings settings, PlacementResult placement)
    {
        InitializeComponent();
        _settings = settings;
        _placement = placement;
        _sim = new RopeSim(Vec2.Zero, placement.RopeLength, settings.SwingStrength);

        UseStyle(settings.Style);
        Rope.RopeStyle = settings.Rope;

        SourceInitialized += OnSourceInitialized;
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(() => DisplayChanged?.Invoke());

        _hoverTimer = new DispatcherTimer(HoverDelay, DispatcherPriority.Normal, OnHoverTimer, Dispatcher) { IsEnabled = false };
        Host.MouseEnter += (_, _) => _hoverTimer.Start();
        Host.MouseLeave += (_, _) => HideHover();
        Host.PreviewMouseLeftButtonDown += (_, _) => HideHover();
    }

    /// <summary>The ornament currently shown (ring or tag).</summary>
    internal OrnamentBase Ornament { get; private set; } = null!;

    /// <summary>The element that receives mouse input for whichever ornament is shown.</summary>
    internal FrameworkElement OrnamentHost => Host;

    internal HoverCard Card => HoverContent;

    internal Canvas Surface => Root;

    internal RopeSim Sim => _sim;

    public PlacementResult Placement => _placement;

    /// <summary>The window's current bounds in device pixels.</summary>
    public ScreenRect WindowRectDevice { get; private set; }

    /// <summary>Where the middle of the ornament is, in window coordinates (for wind).</summary>
    internal Vec2 OrnamentCenter => _sim.OrnamentPosition + new Vec2(0, Ornament.CenterOffset);

    /// <summary>Raised when the window's DPI changes; the owner re-reads the monitors and moves it.</summary>
    public event Action? DisplayChanged;

    /// <summary>Raised after the ornament style is swapped, so its numbers can be filled in again.</summary>
    public event Action? OrnamentReplaced;

    /// <summary>Re-reads look and motion settings (called live from the Settings window).</summary>
    public void ApplySettings()
    {
        UseStyle(_settings.Style);
        Ornament.Scale = _settings.OrnamentScale;
        Rope.RopeStyle = _settings.Rope;
        Rope.InvalidateVisual();
        _sim.SwingStrength = _settings.SwingStrength;
        _placement = _placement with { RopeLength = _settings.RopeLength };
        ApplyPlacement();
        UpdateVisuals();
    }

    /// <summary>Fades the widget out (then hides it) or shows it and fades it in, over 200 ms.</summary>
    public void FadeTo(bool visible)
    {
        var fade = new System.Windows.Media.Animation.DoubleAnimation(visible ? 1 : 0, TimeSpan.FromMilliseconds(200));
        if (visible)
        {
            if (!IsVisible)
            {
                Opacity = 0;
                Show();
            }
        }
        else
        {
            fade.Completed += (_, _) => { if (Opacity == 0) Hide(); };
        }
        BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>A short nervous jitter: six alternating sideways nudges over 0.3 s.</summary>
    public void Shiver()
    {
        var nudges = 0;
        var timer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher) { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) =>
        {
            _sim.ApplyImpulse(new Vec2(nudges % 2 == 0 ? 120 : -120, 0));
            Wake();
            if (++nudges == 6) timer.Stop();
        };
        timer.Start();
    }

    /// <summary>Redraws everything after a theme change.</summary>
    public void Repaint()
    {
        Ornament.InvalidateVisual();
        Rope.InvalidateVisual();
    }

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
        var size = Ornament.DesiredSize;
        var half = size.Width / 2;
        var x = Math.Clamp(hangTarget.X, half, Math.Max(half, Root.Width - half));
        var y = Math.Clamp(hangTarget.Y, 0, Math.Max(0, Root.Height - size.Height));
        return new Vec2(x, y);
    }

    private void UseStyle(OrnamentStyle style)
    {
        var wanted = style == OrnamentStyle.Tag ? typeof(HangingTag) : typeof(RingOrnament);
        if (Ornament?.GetType() == wanted) return;

        Ornament = style == OrnamentStyle.Tag ? new HangingTag() : new RingOrnament();
        Ornament.Scale = _settings.OrnamentScale;
        Host.Child = Ornament;
        Host.UpdateLayout();
        OrnamentReplaced?.Invoke();
    }

    private void OnHoverTimer(object? sender, EventArgs e)
    {
        _hoverTimer.Stop();
        if (Host.IsMouseOver && Mouse.LeftButton == MouseButtonState.Released) Hover.IsOpen = true;
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
        var width = Ornament.DesiredSize.Width > 0 ? Ornament.DesiredSize.Width : Host.ActualWidth;
        Canvas.SetLeft(Host, hang.X - width / 2);
        Canvas.SetTop(Host, hang.Y);
        Host.RenderTransform = new RotateTransform(_sim.OrnamentAngle * 180 / Math.PI, width / 2, 0);
    }
}
