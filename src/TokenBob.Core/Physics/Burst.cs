namespace TokenBob.Core.Physics;

public enum BurstKind { Arc, Shard, Spark, Confetti, Pop }

/// <summary>What a piece is painted with; the app maps these to its palette.</summary>
public enum BurstPaint { Card, Track, Grey, White, Gold, Green, Amber, Red }

/// <summary>A piece of the ornament, placed by its offset from the ornament's centre (unrotated).</summary>
public abstract record BurstPiece(Vec2 Offset, BurstPaint Paint);

/// <summary>A bit of a ring: an arc of <paramref name="SpanDegrees"/> whose midpoint sits at the offset.</summary>
public sealed record BurstArc(Vec2 Offset, double Radius, double SpanDegrees, double Thickness, BurstPaint Paint) : BurstPiece(Offset, Paint);

/// <summary>A bit of the card: a rounded rectangle centred on the offset.</summary>
public sealed record BurstShard(Vec2 Offset, double Width, double Height, double CornerRadius, BurstPaint Paint) : BurstPiece(Offset, Paint);

public sealed class BurstParticle
{
    public BurstKind Kind { get; init; }
    public BurstPaint Paint { get; init; }
    public Vec2 Position { get; set; }
    public Vec2 Previous { get; set; }
    public Vec2 Velocity { get; set; }
    public double Rotation { get; set; }
    public double Spin { get; init; }
    public double Age { get; set; }
    public double Life { get; init; }
    public double Phase { get; init; }
    /// <summary>Arcs: the piece's offset from the ornament centre, so the arc keeps its curve.</summary>
    public Vec2 Offset { get; init; }
    public double Radius { get; init; }
    public double SpanDegrees { get; init; }
    public double Thickness { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }
    public double CornerRadius { get; init; }
    /// <summary>Sparks and pops: dot radius.</summary>
    public double Size { get; init; }
    /// <summary>Sparks: downward pull in px/s² (negative lets the arrival sparkles drift up).</summary>
    public double Gravity { get; init; } = Burst.SparkGravity;

    public double Progress => Math.Clamp(Age / Life, 0, 1);

    public double Opacity => Math.Clamp(1 - Progress * Progress, 0, 1);
}

/// <summary>An expanding shock ring, or a soft bloom when <see cref="Ring"/> is false.</summary>
public sealed class BurstFlash
{
    public Vec2 Center { get; set; }
    public BurstPaint Paint { get; init; }
    public bool Ring { get; init; }
    public double MaxRadius { get; init; }
    public double Age { get; set; }
    public double Life { get; init; }

    public double Progress => Math.Clamp(Age / Life, 0, 1);
}

/// <summary>
/// The particles of the reset blast: the ornament's own pieces fly apart with sparks, then
/// a few firecrackers pop around where it was, throwing sparks and confetti. Positions are
/// window coordinates; the app only draws what is here.
/// </summary>
public sealed class Burst
{
    public const double Gravity = 900;               // px/s² for pieces (lighter than the rope, so they float a little)
    public const double SparkGravity = 500;
    private const double SparkleLift = -20;          // the arrival sparkles float up instead of falling
    private const double ConfettiGravity = 140;
    private const int CrackerCount = 5;
    private static readonly BurstPaint[] CrackerPaints = [BurstPaint.Gold, BurstPaint.Green, BurstPaint.Amber, BurstPaint.White, BurstPaint.Red];

    private readonly Random _random;
    private readonly List<BurstParticle> _particles = new();
    private readonly List<BurstFlash> _flashes = new();
    private readonly List<Cracker> _crackers = new();

    private sealed record Cracker(double Delay, Vec2 Center, BurstPaint Paint)
    {
        public double Delay { get; set; } = Delay;
    }

    public Burst(Random? random = null) => _random = random ?? Random.Shared;

    public IReadOnlyList<BurstParticle> Particles => _particles;

    public IReadOnlyList<BurstFlash> Flashes => _flashes;

    /// <summary>Firecrackers scheduled but not popped yet.</summary>
    public int Pending => _crackers.Count;

    public bool IsActive => _particles.Count > 0 || _flashes.Count > 0 || _crackers.Count > 0;

    /// <summary>
    /// Blows the ornament apart. <paramref name="center"/> is the ornament's centre in window
    /// coordinates and <paramref name="angle"/> its rotation (radians, clockwise), so every piece
    /// starts exactly where it was drawn.
    /// </summary>
    public void Shatter(IEnumerable<BurstPiece> pieces, Vec2 center, double angle, BurstPaint accent = BurstPaint.Gold)
    {
        foreach (var piece in pieces)
        {
            var at = center + piece.Offset.Rotated(angle);
            var direction = piece.Offset.Length > 1e-6 ? piece.Offset.Rotated(angle).Normalized() : RandomDirection();
            var velocity = direction * Range(240, 520) + new Vec2(Range(-120, 120), Range(-120, 120) - 160);
            _particles.Add(piece switch
            {
                BurstArc arc => new BurstParticle
                {
                    Kind = BurstKind.Arc, Paint = arc.Paint, Position = at, Previous = at, Velocity = velocity,
                    Rotation = angle, Spin = Range(-9, 9), Life = Range(0.7, 1.1),
                    Offset = arc.Offset, Radius = arc.Radius, SpanDegrees = arc.SpanDegrees, Thickness = arc.Thickness,
                },
                BurstShard shard => new BurstParticle
                {
                    Kind = BurstKind.Shard, Paint = shard.Paint, Position = at, Previous = at, Velocity = velocity,
                    Rotation = angle, Spin = Range(-9, 9), Life = Range(0.7, 1.1), Phase = Range(0, Math.Tau),
                    Width = shard.Width, Height = shard.Height, CornerRadius = shard.CornerRadius,
                },
                _ => throw new ArgumentException($"Unknown piece {piece.GetType().Name}", nameof(pieces)),
            });
        }

        Sparks(center, 18, 10, 40, 300, 700, accent);
        _flashes.Add(new BurstFlash { Center = center, Paint = accent, Ring = true, MaxRadius = 120, Life = 0.38 });
        _flashes.Add(new BurstFlash { Center = center, Paint = BurstPaint.White, Ring = false, MaxRadius = 50, Life = 0.22 });

        for (var i = 0; i < CrackerCount; i++)
        {
            var at = center + new Vec2(Range(-75, 75), Range(-85, 25));
            _crackers.Add(new Cracker(0.06 + i * 0.11 + Range(0, 0.05), at, CrackerPaints[_random.Next(CrackerPaints.Length)]));
        }
    }

    /// <summary>The new ornament has arrived: a soft ring, rising sparkles and a little confetti.</summary>
    public void Celebrate(Vec2 center, double radius, BurstPaint paint = BurstPaint.Green)
    {
        _flashes.Add(new BurstFlash { Center = center, Paint = paint, Ring = true, MaxRadius = radius + 40, Life = 0.45 });
        for (var i = 0; i < 14; i++)
        {
            var at = center + RandomDirection() * Range(radius * 0.7, radius * 1.2);
            _particles.Add(new BurstParticle
            {
                Kind = BurstKind.Spark, Paint = i % 4 == 0 ? BurstPaint.White : paint, Position = at, Previous = at,
                Velocity = new Vec2(Range(-30, 30), Range(-70, -20)), Life = Range(0.5, 0.8), Size = Range(1.5, 3), Gravity = SparkleLift,
            });
        }
        Confetti(center, 10, radius, paint);
    }

    public void Advance(double elapsedSeconds)
    {
        var dt = Math.Max(0, elapsedSeconds);
        if (dt == 0) return;

        for (var i = _crackers.Count - 1; i >= 0; i--)
        {
            _crackers[i].Delay -= dt;
            if (_crackers[i].Delay > 0) continue;
            Pop(_crackers[i]);
            _crackers.RemoveAt(i);
        }

        foreach (var p in _particles)
        {
            p.Age += dt;
            p.Previous = p.Position;
            var v = p.Velocity;
            v = p.Kind switch
            {
                BurstKind.Spark or BurstKind.Pop => v * Math.Pow(0.3, dt) + new Vec2(0, p.Gravity * dt),
                BurstKind.Confetti => v * Math.Pow(0.4, dt) + new Vec2(Math.Sin(p.Age * 9 + p.Phase) * 60 * dt, ConfettiGravity * dt),
                _ => new Vec2(v.X * Math.Pow(0.75, dt), v.Y + Gravity * dt),
            };
            p.Velocity = v;
            p.Position += v * dt;
            p.Rotation += p.Spin * dt;
        }
        _particles.RemoveAll(p => p.Age >= p.Life);

        foreach (var f in _flashes) f.Age += dt;
        _flashes.RemoveAll(f => f.Age >= f.Life);
    }

    public void Clear()
    {
        _particles.Clear();
        _flashes.Clear();
        _crackers.Clear();
    }

    /// <summary>Moves everything by <paramref name="delta"/> (the window it lives in was moved or resized).</summary>
    public void Shift(Vec2 delta)
    {
        foreach (var p in _particles)
        {
            p.Position += delta;
            p.Previous += delta;
        }
        foreach (var f in _flashes) f.Center += delta;
        for (var i = 0; i < _crackers.Count; i++) _crackers[i] = _crackers[i] with { Center = _crackers[i].Center + delta };
    }

    /// <summary>One firecracker: a small flash, a ray of sparks, a few bright pops and confetti.</summary>
    private void Pop(Cracker cracker)
    {
        _flashes.Add(new BurstFlash { Center = cracker.Center, Paint = cracker.Paint, Ring = true, MaxRadius = 42, Life = 0.25 });
        _flashes.Add(new BurstFlash { Center = cracker.Center, Paint = BurstPaint.White, Ring = false, MaxRadius = 18, Life = 0.12 });
        Sparks(cracker.Center, 12, 0, 4, 180, 420, cracker.Paint);
        for (var i = 0; i < 3; i++)
        {
            var at = cracker.Center + RandomDirection() * Range(4, 16);
            _particles.Add(new BurstParticle
            {
                Kind = BurstKind.Pop, Paint = BurstPaint.White, Position = at, Previous = at,
                Velocity = RandomDirection() * Range(20, 60), Life = Range(0.08, 0.14), Size = Range(2.5, 4),
            });
        }
        Confetti(cracker.Center, 6, 10, cracker.Paint);
    }

    /// <summary>Sparks leaving a ring around <paramref name="center"/> outward, every third one white.</summary>
    private void Sparks(Vec2 center, int count, double minOffset, double maxOffset, double minSpeed, double maxSpeed, BurstPaint paint)
    {
        for (var i = 0; i < count; i++)
        {
            var direction = RandomDirection();
            var at = center + direction * Range(minOffset, maxOffset);
            _particles.Add(new BurstParticle
            {
                Kind = BurstKind.Spark, Paint = i % 3 == 0 ? BurstPaint.White : paint, Position = at, Previous = at,
                Velocity = direction * Range(minSpeed, maxSpeed) + new Vec2(0, -80), Life = Range(0.35, 0.7), Size = Range(1.5, 3.2),
            });
        }
    }

    private void Confetti(Vec2 center, int count, double spread, BurstPaint paint)
    {
        for (var i = 0; i < count; i++)
        {
            var at = center + RandomDirection() * Range(0, spread);
            _particles.Add(new BurstParticle
            {
                Kind = BurstKind.Confetti, Paint = i % 3 == 0 ? BurstPaint.Gold : paint, Position = at, Previous = at,
                Velocity = new Vec2(Range(-140, 140), Range(-220, -60)), Rotation = Range(0, Math.Tau), Spin = Range(-7, 7),
                Life = Range(0.9, 1.4), Phase = Range(0, Math.Tau), Width = Range(5, 8), Height = 2.6,
            });
        }
    }

    private double Range(double min, double max) => min + _random.NextDouble() * (max - min);

    private Vec2 RandomDirection()
    {
        var a = Range(0, Math.Tau);
        return new Vec2(Math.Cos(a), Math.Sin(a));
    }
}
