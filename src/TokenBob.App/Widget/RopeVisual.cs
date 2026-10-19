using System.Windows;
using System.Windows.Media;
using TokenBob.Core.Config;
using TokenBob.Core.Physics;

namespace TokenBob.App.Widget;

/// <summary>Holds the latest rope points and redraws them.</summary>
internal sealed class RopeVisual : FrameworkElement
{
    private readonly Vec2[] _points = new Vec2[RopeSim.PointCount];
    private int _count;

    public RopeStyle RopeStyle { get; set; } = RopeStyle.TwistedCord;

    public CordColor Cord { get; set; } = CordColor.Gold;

    public void Update(IReadOnlyList<Vec2> points)
    {
        _count = Math.Min(points.Count, _points.Length);
        for (var i = 0; i < _count; i++) _points[i] = points[i];
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc) => RopeRenderer.Draw(dc, new ArraySegment<Vec2>(_points, 0, _count), RopeStyle, Cord);
}
