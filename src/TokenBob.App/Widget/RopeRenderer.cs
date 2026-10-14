using System.Windows;
using System.Windows.Media;
using TokenBob.Core.Config;
using TokenBob.Core.Physics;

namespace TokenBob.App.Widget;

/// <summary>Draws the rope procedurally: no textures, sharp at any DPI.</summary>
internal static class RopeRenderer
{
    private const double CordWidth = 6;
    private static readonly Pen CordShadowPen = RoundPen(Palette.Shadow, CordWidth);
    private static readonly Pen CordBasePen = RoundPen(Palette.CordBase, CordWidth);
    private static readonly Pen CordStripePen = FlatPen(Palette.CordStripe, 1.6);
    private static readonly Pen CordHighlightPen = RoundPen(Palette.CordHighlight, 1);
    private static readonly Pen ChainLinkPen = RoundPen(Palette.BeadLink, 1.1);
    private static readonly Pen BeadRimPen = FlatPen(Palette.BeadRim, 0.5);
    private const double BeadRadius = 3.4;
    private const double BeadGap = 8.2;

    public static void Draw(DrawingContext dc, IReadOnlyList<Vec2> points, RopeStyle style)
    {
        if (points.Count < 2) return;
        if (style == RopeStyle.BeadChain) DrawBeadChain(dc, points);
        else DrawCord(dc, points);
    }

    /// <summary>A metal ball chain like a lamp pull: shaded beads on a thin link, with a clasp at the end.</summary>
    private static void DrawBeadChain(DrawingContext dc, IReadOnlyList<Vec2> points)
    {
        // Start one gap below the mount and stop short of the clasp so beads never pile up.
        var beads = Samples(points, BeadGap).Skip(1).Select(s => s.At).ToList();
        var end = points[^1];
        var tail = (end - points[^2]).Normalized();
        if (beads.Count > 0 && (beads[^1] - end).Length < BeadGap * 0.8) beads.RemoveAt(beads.Count - 1);

        foreach (var bead in beads)
            dc.DrawEllipse(Palette.Shadow, null, new Point(bead.X + 2, bead.Y + 2.5), BeadRadius, BeadRadius);

        dc.DrawGeometry(null, ChainLinkPen, SmoothPath(points));

        foreach (var bead in beads)
            dc.DrawEllipse(Palette.BeadMetal, BeadRimPen, ToPoint(bead), BeadRadius, BeadRadius);

        DrawClasp(dc, end, tail);
        dc.DrawRoundedRectangle(Palette.BeadMetal, BeadRimPen, new Rect(points[0].X - 6, points[0].Y - 2, 12, 6), 2, 2);
    }

    /// <summary>The small metal sleeve that joins the chain to the ornament.</summary>
    private static void DrawClasp(DrawingContext dc, Vec2 end, Vec2 direction)
    {
        var angle = Math.Atan2(direction.Y, direction.X) * 180 / Math.PI - 90;
        dc.PushTransform(new RotateTransform(angle, end.X, end.Y));
        dc.DrawRoundedRectangle(Palette.BeadMetal, BeadRimPen, new Rect(end.X - 2.6, end.Y - 9, 5.2, 9), 2.2, 2.2);
        dc.Pop();
    }

    private static void DrawCord(DrawingContext dc, IReadOnlyList<Vec2> points)
    {
        var path = SmoothPath(points);

        dc.PushTransform(new TranslateTransform(3, 3));
        dc.DrawGeometry(null, CordShadowPen, path);
        dc.Pop();

        dc.DrawGeometry(null, CordBasePen, path);

        foreach (var (at, normal, tangent) in Samples(points, 5))
        {
            var a = at + normal * 3 - tangent * 1.8;
            var b = at - normal * 3 + tangent * 1.8;
            dc.DrawLine(CordStripePen, ToPoint(a), ToPoint(b));
        }

        dc.PushTransform(new TranslateTransform(-1.4, -0.6));
        dc.DrawGeometry(null, CordHighlightPen, path);
        dc.Pop();

        dc.DrawEllipse(Palette.CordStripe, null, ToPoint(points[0]), 3.5, 3.5);
    }

    /// <summary>Quadratic curve through the midpoints, so the rope bends smoothly.</summary>
    private static StreamGeometry SmoothPath(IReadOnlyList<Vec2> points)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(ToPoint(points[0]), isFilled: false, isClosed: false);
            for (var i = 1; i < points.Count - 1; i++)
            {
                var mid = (points[i] + points[i + 1]) / 2;
                ctx.QuadraticBezierTo(ToPoint(points[i]), ToPoint(mid), isStroked: true, isSmoothJoin: true);
            }
            ctx.LineTo(ToPoint(points[^1]), isStroked: true, isSmoothJoin: true);
        }
        geometry.Freeze();
        return geometry;
    }

    /// <summary>Evenly spaced points along the rope with their unit normal and tangent.</summary>
    private static IEnumerable<(Vec2 At, Vec2 Normal, Vec2 Tangent)> Samples(IReadOnlyList<Vec2> points, double gap)
    {
        var carried = 0.0;
        for (var i = 0; i < points.Count - 1; i++)
        {
            var segment = points[i + 1] - points[i];
            var length = segment.Length;
            if (length < 1e-6) continue;
            var tangent = segment / length;
            var normal = new Vec2(-tangent.Y, tangent.X);
            for (var d = carried; d < length; d += gap)
                yield return (points[i] + tangent * d, normal, tangent);
            carried = (carried - length) % gap;
            if (carried < 0) carried += gap;
        }
    }

    private static Point ToPoint(Vec2 v) => new(v.X, v.Y);

    private static Pen RoundPen(Brush brush, double width)
    {
        var pen = new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        pen.Freeze();
        return pen;
    }

    private static Pen FlatPen(Brush brush, double width)
    {
        var pen = new Pen(brush, width);
        pen.Freeze();
        return pen;
    }
}
