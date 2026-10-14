using System.Globalization;
using System.Windows;
using System.Windows.Media;
using TokenBob.Core.Alerts;

namespace TokenBob.App.Widget.Styles;

/// <summary>
/// Style A: a round badge with two rings — outer = weekly, inner = 5-hour — and the
/// 5-hour % in the middle. Its hang point is the top center of the element.
/// </summary>
internal sealed class RingOrnament : FrameworkElement
{
    private const double Diameter = 80;
    private const double LoopRadius = 3.5;
    private const double LoopHeight = 9;
    private const double ShadowOffset = 3;

    public static readonly DependencyProperty FiveHourPercentProperty = Register(nameof(FiveHourPercent), 0.0);
    public static readonly DependencyProperty WeeklyPercentProperty = Register(nameof(WeeklyPercent), 0.0);
    public static readonly DependencyProperty CenterTextProperty = Register(nameof(CenterText), "");
    public static readonly DependencyProperty FiveHourLevelProperty = Register(nameof(FiveHourLevel), ColorLevel.Green);
    public static readonly DependencyProperty WeeklyLevelProperty = Register(nameof(WeeklyLevel), ColorLevel.Green);
    public static readonly DependencyProperty IsGreyProperty = Register(nameof(IsGrey), false);
    public static readonly DependencyProperty ScaleProperty = DependencyProperty.Register(
        nameof(Scale), typeof(double), typeof(RingOrnament),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double FiveHourPercent { get => (double)GetValue(FiveHourPercentProperty); set => SetValue(FiveHourPercentProperty, value); }
    public double WeeklyPercent { get => (double)GetValue(WeeklyPercentProperty); set => SetValue(WeeklyPercentProperty, value); }
    public string CenterText { get => (string)GetValue(CenterTextProperty); set => SetValue(CenterTextProperty, value); }
    public ColorLevel FiveHourLevel { get => (ColorLevel)GetValue(FiveHourLevelProperty); set => SetValue(FiveHourLevelProperty, value); }
    public ColorLevel WeeklyLevel { get => (ColorLevel)GetValue(WeeklyLevelProperty); set => SetValue(WeeklyLevelProperty, value); }
    public bool IsGrey { get => (bool)GetValue(IsGreyProperty); set => SetValue(IsGreyProperty, value); }
    public double Scale { get => (double)GetValue(ScaleProperty); set => SetValue(ScaleProperty, value); }

    /// <summary>Distance from the hang point to the middle of the badge (where people grab it).</summary>
    public double CenterOffset => LoopHeight + Diameter / 2 * Scale;

    protected override Size MeasureOverride(Size availableSize) =>
        new(Diameter * Scale + ShadowOffset, LoopHeight + Diameter * Scale + ShadowOffset);

    protected override void OnRender(DrawingContext dc)
    {
        var s = Scale;
        var cx = Diameter * s / 2;
        var center = new Point(cx, CenterOffset);
        var radius = Diameter / 2 * s;

        dc.DrawEllipse(null, new Pen(Palette.Text, 1.5), new Point(cx, LoopRadius + 0.5), LoopRadius, LoopRadius);
        dc.DrawEllipse(Palette.Shadow, null, new Point(center.X + ShadowOffset, center.Y + ShadowOffset), radius, radius);
        dc.DrawEllipse(Palette.Card, null, center, radius, radius);

        DrawRing(dc, center, 33 * s, 6 * s, WeeklyPercent, Palette.ForLevel(WeeklyLevel, IsGrey));
        DrawRing(dc, center, 24 * s, 6 * s, FiveHourPercent, Palette.ForLevel(FiveHourLevel, IsGrey));

        var text = new FormattedText(CenterText, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            14 * s, IsGrey ? Palette.Grey : Palette.Text, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
    }

    private static void DrawRing(DrawingContext dc, Point center, double radius, double thickness, double percent, Brush color)
    {
        dc.DrawEllipse(null, new Pen(Palette.Track, thickness), center, radius, radius);

        var share = Math.Clamp(percent, 0, 100) / 100;
        if (share <= 0) return;

        var pen = new Pen(color, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (share >= 0.999)
        {
            dc.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        var start = PointOnCircle(center, radius, 0);
        var end = PointOnCircle(center, radius, share * 360);
        var arc = new StreamGeometry();
        using (var ctx = arc.Open())
        {
            ctx.BeginFigure(start, isFilled: false, isClosed: false);
            ctx.ArcTo(end, new Size(radius, radius), 0, share > 0.5, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
        }
        arc.Freeze();
        dc.DrawGeometry(null, pen, arc);
    }

    /// <summary>Angle in degrees, clockwise from 12 o'clock.</summary>
    private static Point PointOnCircle(Point center, double radius, double degrees)
    {
        var rad = degrees * Math.PI / 180;
        return new Point(center.X + radius * Math.Sin(rad), center.Y - radius * Math.Cos(rad));
    }

    private static DependencyProperty Register<T>(string name, T defaultValue) =>
        DependencyProperty.Register(name, typeof(T), typeof(RingOrnament),
            new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.AffectsRender));
}
