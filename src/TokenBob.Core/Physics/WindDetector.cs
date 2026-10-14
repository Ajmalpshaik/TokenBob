namespace TokenBob.Core.Physics;

/// <summary>A cursor rushing past the ornament gives it a little sideways push.</summary>
public sealed class WindDetector
{
    public const double Radius = 80;            // logical px
    public const double SpeedThreshold = 1200;  // logical px/s
    public static readonly TimeSpan Cooldown = TimeSpan.FromMilliseconds(300);
    private const double Strength = 0.15;

    private (Vec2 At, TimeSpan Time)? _previous;
    private TimeSpan? _lastGust;

    /// <returns>The velocity impulse to apply to the ornament, or null for no gust.</returns>
    public Vec2? Sample(Vec2 cursor, Vec2 ornament, TimeSpan t)
    {
        var previous = _previous;
        _previous = (cursor, t);
        if (previous is not { } prev || t <= prev.Time) return null;

        var velocity = (cursor - prev.At) / (t - prev.Time).TotalSeconds;
        if (velocity.Length < SpeedThreshold || (cursor - ornament).Length > Radius) return null;
        if (_lastGust is { } last && t - last < Cooldown) return null;

        _lastGust = t;
        return new Vec2(velocity.X * Strength, 0);
    }
}
