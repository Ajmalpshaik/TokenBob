using System.Windows;
using System.Windows.Media;
using TokenBob.Core.Config;
using TokenBob.Core.Physics;

namespace TokenBob.App.Widget;

/// <summary>Holds the latest rope points and redraws them.</summary>
internal sealed class RopeVisual : FrameworkElement
{
    private Vec2[] _points = [];

    public RopeStyle RopeStyle { get; set; } = RopeStyle.TwistedCord;

    public void Update(IReadOnlyList<Vec2> points)
    {
        _points = points.ToArray();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc) => RopeRenderer.Draw(dc, _points, RopeStyle);
}
