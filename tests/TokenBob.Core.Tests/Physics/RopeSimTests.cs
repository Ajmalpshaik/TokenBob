using TokenBob.Core.Physics;

namespace TokenBob.Core.Tests.Physics;

public class RopeSimTests
{
    private static RopeSim Sim() => new(new Vec2(200, 0), 150, 0.5);

    private static void Run(RopeSim s, double seconds)
    {
        for (double t = 0; t < seconds; t += 1 / 60.0) s.Advance(1 / 60.0);
    }

    [Fact]
    public void Settles_to_rest_within_5_seconds_after_fling()
    {
        var s = Sim();
        Run(s, 1);
        s.ApplyImpulse(new Vec2(1500, 0));
        Run(s, 5);
        Assert.True(s.IsAtRest);
        Assert.InRange(s.OrnamentPosition.X, 199, 201);
    }

    [Fact]
    public void Holds_rest_length_within_2_percent_at_rest()
    {
        var s = Sim();
        Run(s, 5);
        Assert.InRange(s.StretchRatio, -0.02, 0.02);
    }

    [Fact]
    public void Hard_pull_exceeds_pull_to_refresh_threshold()
    {
        var s = Sim();
        Run(s, 1);
        s.Grab(new Vec2(200, 150 * 1.6));
        Run(s, 0.5);
        Assert.True(s.StretchRatio > 0.25, $"stretch was {s.StretchRatio}");
    }

    [Fact]
    public void Ornament_angle_converges_to_rope_angle()
    {
        var s = Sim();
        s.ApplyImpulse(new Vec2(1500, 0));
        Run(s, 5);
        Assert.InRange(s.OrnamentAngle, -0.01, 0.01);
    }

    [Fact]
    public void Impulse_wakes_a_resting_rope()
    {
        var s = Sim();
        Run(s, 5);
        s.ApplyImpulse(new Vec2(300, 0));
        s.Advance(1 / 60.0);
        Assert.False(s.IsAtRest);
    }

    [Fact]
    public void Freeze_holds_rope_straight_down()
    {
        var s = Sim();
        s.ApplyImpulse(new Vec2(1500, 0));
        s.Freeze(true);
        s.Advance(0.1);
        Assert.Equal(200, s.OrnamentPosition.X, 3);
        Assert.Equal(150, s.OrnamentPosition.Y, 1);
    }

    [Fact]
    public void Large_time_gap_stays_finite()
    {
        var s = Sim();
        s.ApplyImpulse(new Vec2(1500, 0));
        s.Advance(30);
        Assert.All(s.Points, p =>
        {
            Assert.True(double.IsFinite(p.X) && double.IsFinite(p.Y));
            Assert.True((p - s.Anchor).Length < 300);
        });
    }
}
