using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TokenBob.App.Widget;
using TokenBob.App.Widget.Styles;
using TokenBob.Core.Config;
using TokenBob.Core.Physics;
using TokenBob.Core.Screens;
using TokenBob.Core.Usage;

namespace TokenBob.App.Settings;

/// <summary>
/// The little stage at the top of Settings: the ornament on its rope, drawn with the current
/// look and the real numbers, posed mid-swing. Redrawn whenever a setting changes.
/// </summary>
internal sealed class OrnamentPreview : Canvas
{
    private const double MinRope = 30, MaxRope = 80;
    private static readonly UsageDisplayModel Sample = SampleModel();

    private readonly RopeVisual _rope = new();
    private OrnamentBase? _ornament;
    private WidgetSettings? _settings;
    private UsageDisplayModel? _model;

    public OrnamentPreview()
    {
        ClipToBounds = true;
        Children.Add(_rope);
        SizeChanged += (_, _) => Refresh();
    }

    public void Show(WidgetSettings settings, UsageDisplayModel? model)
    {
        _settings = settings;
        _model = model ?? _model;
        Refresh();
    }

    /// <summary>Redraws with the current palette (after a theme change).</summary>
    public void Repaint() => Refresh();

    private void Refresh()
    {
        if (_settings is not { } s || ActualWidth <= 0 || ActualHeight <= 0) return;

        var rope = MinRope + (s.RopeLength - Placement.MinRope) / (Placement.MaxRope - Placement.MinRope) * (MaxRope - MinRope);
        var sim = new RopeSim(new Vec2(ActualWidth / 2, 0), rope, s.SwingStrength);
        for (var t = 0; t < 60; t++) sim.Advance(1 / 60.0);
        sim.ApplyImpulse(new Vec2(360, 0));
        for (var t = 0; t < 12; t++) sim.Advance(1 / 60.0);

        _rope.Width = ActualWidth;
        _rope.Height = ActualHeight;
        _rope.RopeStyle = s.Rope;
        _rope.Cord = s.Cord;
        _rope.Opacity = s.Opacity;
        _rope.Update(sim.Points);

        var wanted = s.Style == OrnamentStyle.Tag ? typeof(HangingTag) : typeof(RingOrnament);
        if (_ornament?.GetType() != wanted)
        {
            if (_ornament is not null) Children.Remove(_ornament);
            _ornament = s.Style == OrnamentStyle.Tag ? new HangingTag() : new RingOrnament();
            Children.Add(_ornament);
        }
        _ornament.Scale = s.OrnamentScale * 0.85;
        _ornament.Opacity = s.Opacity;
        UsagePresenter.Fill(_ornament, _model ?? Sample, s.Center);
        _ornament.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var hang = sim.OrnamentPosition;
        var width = _ornament.DesiredSize.Width;
        SetLeft(_ornament, hang.X - width / 2);
        SetTop(_ornament, hang.Y);
        _ornament.RenderTransform = new RotateTransform(sim.OrnamentAngle * 180 / Math.PI, width / 2, 0);
        _ornament.InvalidateVisual();
    }

    private static UsageDisplayModel SampleModel()
    {
        var now = DateTimeOffset.Now;
        var snapshot = new UsageSnapshot(new LimitUsage(42, now.AddHours(2).AddMinutes(10)), new LimitUsage(71, now.AddDays(4)), now);
        return UsageDisplay.From(new UsageState(snapshot, null, false), now, TimeZoneInfo.Local, System.Globalization.CultureInfo.CurrentCulture);
    }
}
