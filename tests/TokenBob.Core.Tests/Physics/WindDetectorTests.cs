using TokenBob.Core.Physics;

namespace TokenBob.Core.Tests.Physics;

public class WindDetectorTests
{
    private static TimeSpan Ms(int ms) => TimeSpan.FromMilliseconds(ms);
    private static readonly Vec2 Ornament = new(100, 110);

    [Fact]
    public void Fast_cursor_near_ornament_gives_impulse()
    {
        var wind = new WindDetector();
        Assert.Null(wind.Sample(new Vec2(40, 100), Ornament, Ms(0)));

        var gust = wind.Sample(new Vec2(100, 100), Ornament, Ms(40));   // 1500 px/s

        Assert.NotNull(gust);
        Assert.Equal(225, gust!.Value.X, 3);
        Assert.Equal(0, gust.Value.Y);
    }

    [Fact]
    public void Slow_or_far_cursor_gives_none()
    {
        var slow = new WindDetector();
        slow.Sample(new Vec2(90, 100), Ornament, Ms(0));
        Assert.Null(slow.Sample(new Vec2(100, 100), Ornament, Ms(40)));   // 250 px/s

        var far = new WindDetector();
        far.Sample(new Vec2(40, 300), Ornament, Ms(0));
        Assert.Null(far.Sample(new Vec2(100, 300), Ornament, Ms(40)));    // fast but 190 px away
    }

    [Fact]
    public void Second_gust_within_cooldown_is_ignored()
    {
        var wind = new WindDetector();
        wind.Sample(new Vec2(40, 100), Ornament, Ms(0));
        Assert.NotNull(wind.Sample(new Vec2(100, 100), Ornament, Ms(40)));
        Assert.Null(wind.Sample(new Vec2(160, 100), Ornament, Ms(80)));
        wind.Sample(new Vec2(40, 100), Ornament, Ms(360));
        Assert.NotNull(wind.Sample(new Vec2(100, 100), Ornament, Ms(400)));
    }
}
