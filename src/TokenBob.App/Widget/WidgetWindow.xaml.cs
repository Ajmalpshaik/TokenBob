using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using TokenBob.App.Widget.Styles;
using TokenBob.Core.Config;
using TokenBob.Core.Physics;
using TokenBob.Core.Screens;
using static TokenBob.App.Platform.NativeMethods;

namespace TokenBob.App.Widget;

/// <summary>
/// The transparent, always-on-top window the ornament hangs in. It is only as big as
/// the rope can reach, sits on one monitor, and never takes keyboard focus.
/// </summary>
public partial class WidgetWindow : Window
{
    /// <summary>How far the rope can be pulled, as a share of its length (pull to refresh needs 125 %).</summary>
    private const double MaxStretch = 1.4;
    /// <summary>Extra window room while the reset pieces fly, so they aren't cut off at the edge.</summary>
    private const double BlastRoom = 160;
    private static readonly TimeSpan HoverDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(3);

    private readonly WidgetSettings _settings;
    private readonly RopeSim _sim;
    private readonly Stopwatch _clock = new();
    private readonly DispatcherTimer _hoverTimer;
    private readonly DispatcherTimer _idleTimer;
    private readonly ScaleTransform _ornamentScale = new();
    private readonly RotateTransform _ornamentRotation = new();
    private PlacementResult _placement;
    private IntPtr _hwnd;
    private bool _rendering;
    private TimeSpan _lastFrame;
    private ResetTimeline? _reset;
    private Action? _applyOnBlast;
    private DropShadowEffect? _glow;
    private double _extraRoom;
    private bool _shown = true;

    public WidgetWindow(WidgetSettings settings, PlacementResult placement)
    {
        InitializeComponent();
        _settings = settings;
        _placement = placement;
        _sim = new RopeSim(Vec2.Zero, placement.RopeLength, settings.SwingStrength);

        // Scale (the reset squeeze and pop) and rotation both pivot on the hang point.
        Host.RenderTransform = new TransformGroup { Children = { _ornamentScale, _ornamentRotation } };
        UseStyle(settings.Style);
        Rope.RopeStyle = settings.Rope;
        Rope.Cord = settings.Cord;
        Opacity = settings.Opacity;

        SourceInitialized += OnSourceInitialized;
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(() => DisplayChanged?.Invoke());

        _hoverTimer = new DispatcherTimer(HoverDelay, DispatcherPriority.Normal, OnHoverTimer, Dispatcher) { IsEnabled = false };
        _idleTimer = new DispatcherTimer(IdleDelay, DispatcherPriority.Background, OnIdleTimer, Dispatcher) { IsEnabled = false };
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

    /// <summary>Where the middle of the ornament is, in window coordinates (for wind and the blast).</summary>
    internal Vec2 OrnamentCenter => _sim.OrnamentPosition + new Vec2(0, Ornament.CenterOffset).Rotated(_sim.OrnamentAngle);

    /// <summary>Raised when the window's DPI changes; the owner re-reads the monitors and moves it.</summary>
    public event Action? DisplayChanged;

    /// <summary>Raised after the ornament style is swapped, so its numbers can be filled in again.</summary>
    public event Action? OrnamentReplaced;

    /// <summary>Raised a few seconds after everything has stopped moving (a good moment to give memory back).</summary>
    public event Action? Idle;

    /// <summary>Re-reads look and motion settings (called live from the Settings window).</summary>
    public void ApplySettings()
    {
        UseStyle(_settings.Style);
        Ornament.Scale = _settings.OrnamentScale;
        Host.UpdateLayout();
        Rope.RopeStyle = _settings.Rope;
        Rope.Cord = _settings.Cord;
        Rope.InvalidateVisual();
        _sim.SwingStrength = _settings.SwingStrength;
        _placement = _placement with { RopeLength = _settings.RopeLength };
        if (_shown)
        {
            // A finished fade keeps holding Opacity, so take it back before setting the new level.
            BeginAnimation(OpacityProperty, null);
            Opacity = _settings.Opacity;
        }
        ApplyPlacement();
        UpdateVisuals();
    }

    /// <summary>Fades the widget out (then hides it) or shows it and fades it in, over 200 ms.</summary>
    public void FadeTo(bool visible)
    {
        _shown = visible;
        var fade = new System.Windows.Media.Animation.DoubleAnimation(visible ? _settings.Opacity : 0, TimeSpan.FromMilliseconds(200));
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

    /// <summary>
    /// The limit reset: the ornament squeezes and glows, blasts into pieces with firecrackers,
    /// and a new one springs in on the rope. <paramref name="applyNewNumbers"/> runs at the
    /// moment of the blast, so the old look flies apart and the new one arrives with the new numbers.
    /// </summary>
    public void PlayReset(Action applyNewNumbers)
    {
        if (_reset is not null)
        {
            // Already playing: the newest numbers are what the new ornament should show.
            if (_applyOnBlast is not null) _applyOnBlast = applyNewNumbers;
            else applyNewNumbers();
            return;
        }

        _reset = new ResetTimeline();
        _applyOnBlast = applyNewNumbers;
        _glow = new DropShadowEffect
        {
            ShadowDepth = 0,
            BlurRadius = 30,
            Opacity = 0,
            RenderingBias = RenderingBias.Performance,
            Color = Palette.ColorOf(Palette.ForLevel(Ornament.FiveHourLevel, Ornament.IsGrey)),
        };
        Host.Effect = _glow;
        HideHover();
        SetBlastRoom(true);
        Wake();
    }

    /// <summary>Redraws everything after a theme change.</summary>
    public void Repaint()
    {
        Ornament.InvalidateVisual();
        Rope.InvalidateVisual();
        Blast.InvalidateVisual();
    }

    public void MoveTo(PlacementResult placement)
    {
        _placement = placement;
        ApplyPlacement();
    }

    /// <summary>Starts the render loop if the rope is moving (or was just nudged).</summary>
    public void Wake()
    {
        _idleTimer.Stop();
        if (_rendering) return;
        _rendering = true;
        _clock.Restart();
        _lastFrame = TimeSpan.Zero;
        CompositionTarget.Rendering += OnFrame;
    }

    /// <summary>
    /// Keeps a dragged hang point within the rope's reach, so it feels like a rope being pulled
    /// taut rather than hitting an invisible wall, and the ornament always stays inside the window.
    /// </summary>
    internal Vec2 ClampToReach(Vec2 hangTarget)
    {
        var offset = hangTarget - _sim.Anchor;
        var reach = MaxStretch * _sim.RestLength;
        if (offset.Length > reach) offset = offset.Normalized() * reach;
        var target = _sim.Anchor + offset;
        return new Vec2(target.X, Math.Max(0, target.Y));
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
        if (Host.IsMouseOver && Mouse.LeftButton == MouseButtonState.Released && _settings.HoverCard) Hover.IsOpen = true;
    }

    private void OnIdleTimer(object? sender, EventArgs e)
    {
        _idleTimer.Stop();
        if (!_rendering) Idle?.Invoke();
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

    /// <summary>
    /// Sizes the window to everything the rope can reach: the stretched rope in any direction
    /// plus the ornament hanging (or sticking out sideways) at its end.
    /// </summary>
    private void ApplyPlacement()
    {
        if (_hwnd == IntPtr.Zero) return;

        var rope = _placement.RopeLength;
        var reach = MaxStretch * rope + Ornament.DesiredSize.Height + _extraRoom;
        var rect = Core.Screens.Placement.WindowRect(_placement, 2 * reach, reach);
        SetWindowPos(_hwnd, HWND_TOPMOST, (int)Math.Round(rect.Left), (int)Math.Round(rect.Top),
            (int)Math.Round(rect.Width), (int)Math.Round(rect.Height), SWP_NOACTIVATE);
        WindowRectDevice = rect;

        var scale = _placement.Monitor.Scale;
        Root.Width = rect.Width / scale;
        Root.Height = rect.Height / scale;
        Rope.Width = Blast.Width = Root.Width;
        Rope.Height = Blast.Height = Root.Height;

        var anchor = new Vec2((_placement.AnchorXDevice - rect.Left) / scale, 0);
        Blast.Shift(anchor - _sim.Anchor);
        _sim.Anchor = anchor;
        _sim.RestLength = rope;
        Wake();
    }

    private void SetBlastRoom(bool on)
    {
        var room = on ? BlastRoom : 0;
        if (room == _extraRoom) return;
        _extraRoom = room;
        ApplyPlacement();
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        var elapsed = (now - _lastFrame).TotalSeconds;
        _lastFrame = now;
        _sim.Advance(elapsed);
        AdvanceReset(elapsed);
        Blast.Advance(elapsed);
        UpdateVisuals();

        if (_reset is not null || Blast.IsActive) return;
        if (_extraRoom > 0) SetBlastRoom(false);
        if (_sim.IsAtRest)
        {
            CompositionTarget.Rendering -= OnFrame;
            _rendering = false;
            _idleTimer.Start();
        }
    }

    private void AdvanceReset(double elapsed)
    {
        if (_reset is null) return;
        var step = _reset.Advance(elapsed);
        if (step.BlastNow) OnBlast();
        if (step.ArriveNow) OnArrive();

        var frame = step.Frame;
        _ornamentScale.ScaleX = _ornamentScale.ScaleY = frame.OrnamentScale;
        Host.Visibility = frame.OrnamentVisible ? Visibility.Visible : Visibility.Hidden;
        if (_glow is not null) _glow.Opacity = frame.Glow;

        if (!_reset.IsDone) return;
        _reset = null;
        _glow = null;
        Host.Effect = null;
        _ornamentScale.ScaleX = _ornamentScale.ScaleY = 1;
    }

    private void OnBlast()
    {
        var center = OrnamentCenter;
        Blast.Shatter(Ornament.Shatter(), center, _sim.OrnamentAngle, Palette.PaintFor(Ornament.FiveHourLevel, Ornament.IsGrey));
        _applyOnBlast?.Invoke();
        _applyOnBlast = null;
        _sim.ApplyImpulse(new Vec2(Random.Shared.NextDouble() * 520 - 260, -900));
    }

    private void OnArrive()
    {
        var paint = Palette.PaintFor(Ornament.FiveHourLevel, Ornament.IsGrey);
        if (_glow is not null) _glow.Color = Palette.ColorOf(Palette.ForPaint(paint));
        Blast.Celebrate(OrnamentCenter, Ornament.DesiredSize.Width / 2, paint);
    }

    private void UpdateVisuals()
    {
        Rope.Update(_sim.Points);

        var hang = _sim.OrnamentPosition;
        var width = Ornament.DesiredSize.Width > 0 ? Ornament.DesiredSize.Width : Host.ActualWidth;
        Canvas.SetLeft(Host, hang.X - width / 2);
        Canvas.SetTop(Host, hang.Y);
        _ornamentScale.CenterX = _ornamentRotation.CenterX = width / 2;
        _ornamentRotation.Angle = _sim.OrnamentAngle * 180 / Math.PI;
    }
}
