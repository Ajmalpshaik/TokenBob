namespace TokenBob.Core.Physics;

public enum ResetPhase { Squeeze, Blast, Arrive, Done }

/// <summary>How the ornament looks at one moment of the reset animation.</summary>
public readonly record struct ResetFrame(ResetPhase Phase, double OrnamentScale, double Glow, bool OrnamentVisible);

/// <summary>One advance of the clock: the frame to show, and whether the blast or the arrival happens now.</summary>
public readonly record struct ResetStep(ResetFrame Frame, bool BlastNow, bool ArriveNow);

/// <summary>
/// The clock of the limit-reset animation. The old ornament squeezes and glows, blasts
/// apart (hidden while its pieces fly), then the new one springs in from the rope end.
/// </summary>
public sealed class ResetTimeline
{
    public const double BlastAt = 0.18;             // seconds
    public const double ArriveAt = 0.52;
    public const double Duration = ArriveAt + 1.1;
    private const double SqueezeAmount = 0.15;      // shrinks to 85 %
    private const double SpringDecay = 5;           // the arrival spring: 1 − e^(−decay·t)·cos(freq·t)
    private const double SpringFrequency = 8;
    private const double ArrivalGlowFade = 4;

    private double _t;
    private bool _blasted;
    private bool _arrived;

    public bool IsDone => _t >= Duration;

    public static ResetFrame At(double t)
    {
        if (t < BlastAt)
        {
            var k = t / BlastAt;
            var ease = k * k;
            return new ResetFrame(ResetPhase.Squeeze, 1 - SqueezeAmount * ease, ease, true);
        }
        if (t < ArriveAt) return new ResetFrame(ResetPhase.Blast, 0, 0, false);
        if (t >= Duration) return new ResetFrame(ResetPhase.Done, 1, 0, true);

        var u = t - ArriveAt;
        var scale = Math.Max(0, 1 - Math.Exp(-SpringDecay * u) * Math.Cos(SpringFrequency * u));
        return new ResetFrame(ResetPhase.Arrive, scale, Math.Exp(-ArrivalGlowFade * u), true);
    }

    public ResetStep Advance(double elapsedSeconds)
    {
        _t += Math.Max(0, elapsedSeconds);
        var blastNow = !_blasted && _t >= BlastAt;
        var arriveNow = !_arrived && _t >= ArriveAt;
        _blasted |= blastNow;
        _arrived |= arriveNow;
        return new ResetStep(At(_t), blastNow, arriveNow);
    }
}
