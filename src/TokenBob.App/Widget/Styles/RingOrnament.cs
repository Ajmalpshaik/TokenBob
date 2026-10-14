using System.Windows;
using System.Windows.Media;

namespace TokenBob.App.Widget.Styles;

/// <summary>
/// Style A: a round badge with two rings — outer = weekly, inner = 5-hour — and the
/// 5-hour % in the middle.
/// </summary>
internal sealed class RingOrnament : OrnamentBase
{
    private const double Diameter = 80;
    private const double LoopRadius = 3.5;
    private const double LoopHeight = 9;

    public override double CenterOffset => LoopHeight + Diameter / 2 * Scale;

    protected override Size MeasureOverride(Size availableSize) =>
        new(Diameter * Scale + ShadowOffset, LoopHeight + Diameter * Scale + ShadowOffset);

    protected override void OnRender(DrawingContext dc)
    {
        var s = Scale;
        var cx = Diameter * s / 2;
        var center = new Point(cx, CenterOffset);
        var radius = Diameter / 2 * s;

        dc.DrawEllipse(null, new Pen(Palette.Loop, 1.8), new Point(cx, LoopRadius + 0.5), LoopRadius, LoopRadius);
        dc.DrawEllipse(Palette.Shadow, null, new Point(center.X + ShadowOffset, center.Y + ShadowOffset), radius, radius);
        dc.DrawEllipse(Palette.Card, null, center, radius, radius);

        DrawRing(dc, center, 33 * s, 6 * s, WeeklyPercent, Palette.ForLevel(WeeklyLevel, IsGrey));
        DrawRing(dc, center, 24 * s, 6 * s, FiveHourPercent, Palette.ForLevel(FiveHourLevel, IsGrey));

        var text = Text(CenterText, 14 * s, IsGrey ? Palette.Grey : Palette.Text, semiBold: true);
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

        var arc = new StreamGeometry();
        using (var ctx = arc.Open())
        {
            ctx.BeginFigure(PointOnCircle(center, radius, 0), isFilled: false, isClosed: false);
            ctx.ArcTo(PointOnCircle(center, radius, share * 360), new Size(radius, radius), 0, share > 0.5,
                SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
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
}
