using System.Windows;
using System.Windows.Media;
using TokenBob.Core.Physics;

namespace TokenBob.App.Widget;

/// <summary>
/// Draws the limit-reset blast over the whole widget window: the ornament's pieces, the
/// shock rings, the firecrackers and their sparks and confetti. The motion lives in
/// <see cref="Burst"/>; this only paints it.
/// </summary>
internal sealed class ResetBlast : FrameworkElement
{
    private const double SparkTrailSeconds = 0.045;   // how far behind a spark its streak reaches
    private static readonly Brush Bloom = FrozenBloom();
    private static readonly Pen Edge = FrozenPen(Palette.Shadow, 0.8);

    private readonly Burst _burst = new();

    public bool IsActive => _burst.IsActive;

    public void Shatter(IEnumerable<BurstPiece> pieces, Vec2 center, double angle, BurstPaint accent)
    {
        _burst.Shatter(pieces, center, angle, accent);
        InvalidateVisual();
    }

    public void Celebrate(Vec2 center, double radius, BurstPaint paint)
    {
        _burst.Celebrate(center, radius, paint);
        InvalidateVisual();
    }

    public void Advance(double elapsedSeconds)
    {
        if (!_burst.IsActive) return;
        _burst.Advance(elapsedSeconds);
        InvalidateVisual();
    }

    public void Shift(Vec2 delta)
    {
        if (!_burst.IsActive) return;
        _burst.Shift(delta);
        InvalidateVisual();
    }

    public void Clear()
    {
        _burst.Clear();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        foreach (var flash in _burst.Flashes) DrawFlash(dc, flash);
        foreach (var particle in _burst.Particles) DrawParticle(dc, particle);
    }

    private static void DrawFlash(DrawingContext dc, BurstFlash flash)
    {
        var p = flash.Progress;
        var center = new Point(flash.Center.X, flash.Center.Y);
        if (flash.Ring)
        {
            // The ring races out fast and thins as it goes.
            var radius = flash.MaxRadius * (0.25 + 0.75 * Math.Sqrt(p));
            dc.PushOpacity((1 - p) * 0.85);
            dc.DrawEllipse(null, new Pen(Palette.ForPaint(flash.Paint), 9 * (1 - p) + 1), center, radius, radius);
            dc.Pop();
        }
        else
        {
            var radius = flash.MaxRadius * (0.6 + 0.4 * p);
            dc.PushOpacity((1 - p) * 0.7);
            dc.DrawEllipse(Bloom, null, center, radius, radius);
            dc.Pop();
        }
    }

    private static void DrawParticle(DrawingContext dc, BurstParticle p)
    {
        var brush = Palette.ForPaint(p.Paint);
        var opacity = p.Opacity;
        if (opacity <= 0) return;

        switch (p.Kind)
        {
            case BurstKind.Spark:
            case BurstKind.Pop:
                DrawSpark(dc, p, brush, opacity);
                return;
        }

        // Shards and confetti tumble: squashing them sideways fakes a flip in depth.
        var flip = p.Kind is BurstKind.Arc ? 1 : 0.22 + 0.78 * Math.Abs(Math.Cos(p.Rotation * 0.9 + p.Phase));
        var matrix = Matrix.Identity;
        matrix.Scale(flip, 1);
        matrix.Rotate(p.Rotation * 180 / Math.PI);
        matrix.Translate(p.Position.X, p.Position.Y);
        dc.PushTransform(new MatrixTransform(matrix));
        dc.PushOpacity(opacity);

        switch (p.Kind)
        {
            case BurstKind.Arc:
                dc.DrawGeometry(null, new Pen(brush, p.Thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, ArcGeometry(p));
                break;
            case BurstKind.Shard:
                dc.DrawRoundedRectangle(brush, Edge, new Rect(-p.Width / 2, -p.Height / 2, p.Width, p.Height), p.CornerRadius, p.CornerRadius);
                break;
            case BurstKind.Confetti:
                dc.DrawRoundedRectangle(brush, null, new Rect(-p.Width / 2, -p.Height / 2, p.Width, p.Height), 1, 1);
                break;
        }

        dc.Pop();
        dc.Pop();
    }

    /// <summary>A bright dot with a short streak behind it; pops get a soft halo instead.</summary>
    private static void DrawSpark(DrawingContext dc, BurstParticle p, Brush brush, double opacity)
    {
        var head = new Point(p.Position.X, p.Position.Y);
        if (p.Kind == BurstKind.Pop)
        {
            dc.PushOpacity(opacity * 0.35);
            dc.DrawEllipse(brush, null, head, p.Size * 2.4, p.Size * 2.4);
            dc.Pop();
        }
        else
        {
            var tail = new Point(head.X - p.Velocity.X * SparkTrailSeconds, head.Y - p.Velocity.Y * SparkTrailSeconds);
            dc.PushOpacity(opacity * 0.7);
            dc.DrawLine(new Pen(brush, p.Size * 1.4) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, tail, head);
            dc.Pop();
        }
        dc.PushOpacity(opacity);
        dc.DrawEllipse(brush, null, head, p.Size * (1 - p.Progress * 0.5), p.Size * (1 - p.Progress * 0.5));
        dc.Pop();
    }

    /// <summary>The arc keeps the curve it had on the ring: its circle centre sits opposite its offset.</summary>
    private static StreamGeometry ArcGeometry(BurstParticle p)
    {
        var direction = p.Offset.Normalized();
        var circle = new Point(-direction.X * p.Radius, -direction.Y * p.Radius);
        var mid = Math.Atan2(direction.X, -direction.Y) * 180 / Math.PI;   // degrees clockwise from 12 o'clock
        var from = PointOnCircle(circle, p.Radius, mid - p.SpanDegrees / 2);
        var to = PointOnCircle(circle, p.Radius, mid + p.SpanDegrees / 2);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(from, isFilled: false, isClosed: false);
            ctx.ArcTo(to, new Size(p.Radius, p.Radius), 0, p.SpanDegrees > 180, SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static Point PointOnCircle(Point center, double radius, double degrees)
    {
        var (sin, cos) = Math.SinCos(degrees * Math.PI / 180);
        return new Point(center.X + radius * sin, center.Y - radius * cos);
    }

    private static Brush FrozenBloom()
    {
        var brush = new RadialGradientBrush(Color.FromArgb(255, 255, 255, 255), Color.FromArgb(0, 255, 255, 255));
        brush.Freeze();
        return brush;
    }

    private static Pen FrozenPen(Brush brush, double width)
    {
        var pen = new Pen(brush, width);
        pen.Freeze();
        return pen;
    }
}
