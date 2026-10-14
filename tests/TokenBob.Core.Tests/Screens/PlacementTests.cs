using TokenBob.Core.Physics;
using TokenBob.Core.Screens;

namespace TokenBob.Core.Tests.Screens;

public class PlacementTests
{
    private static readonly MonitorInfo A = new(@"\\.\DISPLAY1", new ScreenRect(0, 0, 1920, 1080), 1.0, IsPrimary: true);
    private static readonly MonitorInfo B = new(@"\\.\DISPLAY2", new ScreenRect(1920, 0, 2880, 1620), 1.5, IsPrimary: false);
    private static readonly MonitorInfo[] Both = [A, B];

    [Fact]
    public void Resolve_with_no_saved_monitor_uses_primary_top_right()
    {
        var p = Placement.Resolve(Both, null, null, 150);
        Assert.Equal(A, p.Monitor);
        Assert.Equal(1800, p.AnchorXDevice);
        Assert.Equal(150, p.RopeLength);
    }

    [Fact]
    public void Resolve_clamps_rope_length()
    {
        Assert.Equal(400, Placement.Resolve(Both, null, null, 5000).RopeLength);
        Assert.Equal(60, Placement.Resolve(Both, null, null, 5).RopeLength);
    }

    [Fact]
    public void Resolve_uses_saved_monitor_and_anchor()
    {
        var p = Placement.Resolve(Both, Placement.IdOf(B), 3000, 200);
        Assert.Equal(B, p.Monitor);
        Assert.Equal(3000, p.AnchorXDevice);
    }

    [Fact]
    public void Resolve_with_missing_saved_monitor_falls_back_to_primary()
    {
        var gone = new MonitorId(@"\\.\DISPLAY9", new ScreenRect(-1920, 0, 1920, 1080));
        var p = Placement.Resolve(Both, gone, -500, 150);
        Assert.Equal(A, p.Monitor);
        Assert.Equal(1800, p.AnchorXDevice);
    }

    [Fact]
    public void Resolve_clamps_saved_anchor_inside_margins()
    {
        Assert.Equal(4740, Placement.Resolve(Both, Placement.IdOf(B), 4795, 150).AnchorXDevice);
        Assert.Equal(1980, Placement.Resolve(Both, Placement.IdOf(B), 1900, 150).AnchorXDevice);
    }

    [Fact]
    public void Edit_clamps_anchor_at_both_edges()
    {
        var current = Placement.Resolve(Both, null, null, 150);
        Assert.Equal(40, Placement.Edit(Both, current, new Vec2(5, 300), 0).AnchorXDevice);
        Assert.Equal(1880, Placement.Edit(Both, current, new Vec2(1915, 300), 0).AnchorXDevice);
    }

    [Fact]
    public void Edit_clamps_rope_60_to_400()
    {
        var current = Placement.Resolve(Both, null, null, 150);
        Assert.Equal(60, Placement.Edit(Both, current, new Vec2(900, 10), 0).RopeLength);
        Assert.Equal(400, Placement.Edit(Both, current, new Vec2(900, 1000), 0).RopeLength);
        Assert.Equal(250, Placement.Edit(Both, current, new Vec2(900, 290), 40).RopeLength);
    }

    [Fact]
    public void Edit_moves_to_monitor_under_cursor()
    {
        var current = Placement.Resolve(Both, null, null, 150);
        Assert.Equal(B, Placement.Edit(Both, current, new Vec2(3000, 300), 0).Monitor);
    }

    [Fact]
    public void Edit_outside_every_monitor_stays_on_current()
    {
        var current = Placement.Resolve(Both, null, null, 150);
        Assert.Equal(A, Placement.Edit(Both, current, new Vec2(1000, -50), 0).Monitor);
    }

    [Fact]
    public void Edit_uses_target_monitor_scale_for_margin_and_rope()
    {
        var current = Placement.Resolve(Both, null, null, 150);
        var p = Placement.Edit(Both, current, new Vec2(4790, 450), 0);
        Assert.Equal(B, p.Monitor);
        Assert.Equal(4740, p.AnchorXDevice);
        Assert.Equal(300, p.RopeLength);
    }

    [Fact]
    public void WindowRect_is_centered_on_anchor_and_clamped_to_monitor()
    {
        var centered = Placement.WindowRect(new PlacementResult(A, 960, 150), 400, 300);
        Assert.Equal(new ScreenRect(760, 0, 400, 300), centered);

        var nearEdge = Placement.WindowRect(new PlacementResult(A, 1880, 150), 400, 300);
        Assert.Equal(A.BoundsDevice.Right, nearEdge.Right);
        Assert.Equal(400, nearEdge.Width);

        var scaled = Placement.WindowRect(new PlacementResult(B, 3000, 150), 400, 300);
        Assert.Equal(new ScreenRect(2700, 0, 600, 450), scaled);
    }
}
