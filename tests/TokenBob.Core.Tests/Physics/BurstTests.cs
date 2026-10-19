using TokenBob.Core.Physics;

namespace TokenBob.Core.Tests.Physics;

public class BurstTests
{
    private static readonly Vec2 Center = new(200, 150);

    private static Burst Seeded() => new(new Random(7));

    private static BurstPiece Arc(double radius, double midDeg) =>
        new BurstArc(new Vec2(radius * Math.Sin(midDeg * Math.PI / 180), -radius * Math.Cos(midDeg * Math.PI / 180)), radius, 36, 6, BurstPaint.Red);

    private static BurstPiece Shard(double x, double y) => new BurstShard(new Vec2(x, y), 12, 10, 3, BurstPaint.Card);

    private static void Run(Burst b, double seconds)
    {
        for (double t = 0; t < seconds; t += 1 / 60.0) b.Advance(1 / 60.0);
    }

    [Fact]
    public void Shatter_turns_every_piece_into_a_particle_plus_sparks_and_a_flash()
    {
        var burst = Seeded();
        burst.Shatter([Arc(24, 18), Arc(24, 54), Shard(0, 0)], Center, angle: 0);

        Assert.Equal(3, burst.Particles.Count(p => p.Kind is BurstKind.Arc or BurstKind.Shard));
        Assert.True(burst.Particles.Count(p => p.Kind == BurstKind.Spark) >= 10);
        Assert.NotEmpty(burst.Flashes);
        Assert.True(burst.IsActive);
    }

    [Fact]
    public void Pieces_start_where_they_were_on_the_ornament()
    {
        var burst = Seeded();
        burst.Shatter([Shard(10, -20)], Center, angle: 0);
        var shard = burst.Particles.Single(p => p.Kind == BurstKind.Shard);
        Assert.Equal(210, shard.Position.X, 6);
        Assert.Equal(130, shard.Position.Y, 6);
    }

    [Fact]
    public void Pieces_follow_the_ornament_rotation()
    {
        var burst = Seeded();
        burst.Shatter([Shard(0, 40)], Center, angle: Math.PI / 2);   // ornament rotated 90° clockwise
        var shard = burst.Particles.Single(p => p.Kind == BurstKind.Shard);
        Assert.Equal(160, shard.Position.X, 6);
        Assert.Equal(150, shard.Position.Y, 6);
        Assert.Equal(Math.PI / 2, shard.Rotation, 6);
    }

    [Fact]
    public void Pieces_fly_away_from_the_center()
    {
        var burst = Seeded();
        burst.Shatter([Shard(30, 0), Shard(-30, 0)], Center, angle: 0);
        var right = burst.Particles.First(p => p.Kind == BurstKind.Shard && p.Position.X > Center.X);
        var left = burst.Particles.First(p => p.Kind == BurstKind.Shard && p.Position.X < Center.X);
        Assert.True(right.Velocity.X > 100, $"right piece vx={right.Velocity.X}");
        Assert.True(left.Velocity.X < -100, $"left piece vx={left.Velocity.X}");
    }

    [Fact]
    public void Gravity_pulls_pieces_down_and_they_fade_out_and_die()
    {
        var burst = Seeded();
        burst.Shatter([Shard(0, 0)], Center, angle: 0);
        var before = burst.Particles.Single(p => p.Kind == BurstKind.Shard);
        var vy0 = before.Velocity.Y;

        Run(burst, 0.5);
        var after = burst.Particles.Single(p => p.Kind == BurstKind.Shard);
        Assert.True(after.Velocity.Y > vy0 + 300, $"vy went {vy0} → {after.Velocity.Y}");
        Assert.InRange(after.Opacity, 0.01, 0.99);

        Run(burst, 3);
        Assert.Empty(burst.Particles);
        Assert.Empty(burst.Flashes);
        Assert.False(burst.IsActive);
    }

    [Fact]
    public void Firecrackers_pop_after_the_blast_not_all_at_once()
    {
        var burst = Seeded();
        burst.Shatter([Shard(0, 0)], Center, angle: 0);
        var atStart = burst.Particles.Count;

        Run(burst, 0.15);
        var early = burst.Particles.Count;
        Run(burst, 0.5);
        var later = burst.Pending;

        Assert.True(early > atStart, "a cracker should have popped within 150 ms");
        Assert.Equal(0, later);
        Assert.Contains(burst.Particles, p => p.Kind == BurstKind.Confetti);
    }

    [Fact]
    public void Celebrate_adds_floating_sparkles_and_confetti_that_rise()
    {
        var burst = Seeded();
        burst.Celebrate(Center, 50);
        Assert.True(burst.IsActive);
        Assert.All(burst.Particles.Where(p => p.Kind == BurstKind.Spark), p => Assert.True(p.Velocity.Y < 0));

        // They keep drifting up for their whole life instead of falling back like blast sparks.
        Run(burst, 0.4);
        Assert.All(burst.Particles.Where(p => p.Kind == BurstKind.Spark), p => Assert.True(p.Velocity.Y < 0, $"sparkle fell: vy={p.Velocity.Y}"));
        Assert.Contains(burst.Particles, p => p.Kind == BurstKind.Confetti);
        Assert.NotEmpty(burst.Flashes);
    }

    [Fact]
    public void Shift_moves_pieces_flashes_and_pending_crackers()
    {
        var burst = Seeded();
        burst.Shatter([Shard(0, 0)], Center, angle: 0);
        var shard = burst.Particles.Single(p => p.Kind == BurstKind.Shard);
        var flash = burst.Flashes[0];
        var (x, y, fx) = (shard.Position.X, shard.Position.Y, flash.Center.X);

        burst.Shift(new Vec2(-30, 12));

        Assert.Equal(x - 30, shard.Position.X, 6);
        Assert.Equal(y + 12, shard.Position.Y, 6);
        Assert.Equal(fx - 30, flash.Center.X, 6);

        // The crackers pop where the ornament was, so they move too.
        Run(burst, 0.7);
        Assert.All(burst.Flashes.Where(f => f.Ring && f.MaxRadius < 100), f => Assert.InRange(f.Center.X, Center.X - 30 - 80, Center.X - 30 + 80));
    }

    [Fact]
    public void Clear_stops_everything()
    {
        var burst = Seeded();
        burst.Shatter([Shard(0, 0)], Center, angle: 0);
        burst.Clear();
        Assert.False(burst.IsActive);
        Assert.Empty(burst.Particles);
        Assert.Equal(0, burst.Pending);
    }
}
