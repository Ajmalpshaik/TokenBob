namespace TokenBob.Core.Physics;

/// <summary>A 2D vector in logical pixels (y grows downward, like the screen).</summary>
public readonly record struct Vec2(double X, double Y)
{
    public static readonly Vec2 Zero = new(0, 0);

    public double Length => Math.Sqrt(X * X + Y * Y);

    public Vec2 Normalized()
    {
        var len = Length;
        return len > 1e-12 ? new Vec2(X / len, Y / len) : Zero;
    }

    /// <summary>Rotated clockwise on screen by <paramref name="radians"/> (the same sense as WPF's RotateTransform).</summary>
    public Vec2 Rotated(double radians)
    {
        var (sin, cos) = Math.SinCos(radians);
        return new Vec2(X * cos - Y * sin, X * sin + Y * cos);
    }

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);
    public static Vec2 operator *(Vec2 a, double k) => new(a.X * k, a.Y * k);
    public static Vec2 operator *(double k, Vec2 a) => new(a.X * k, a.Y * k);
    public static Vec2 operator /(Vec2 a, double k) => new(a.X / k, a.Y / k);
}
