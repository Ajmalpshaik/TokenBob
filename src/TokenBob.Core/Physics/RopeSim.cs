namespace TokenBob.Core.Physics;

/// <summary>
/// A hanging rope of <see cref="PointCount"/> Verlet points. Point 0 is pinned to
/// <see cref="Anchor"/>; the last point carries the ornament. Advances in fixed
/// steps so the motion is the same at any frame rate.
/// </summary>
public sealed class RopeSim
{
    public const int PointCount = 14;
    public const double FixedStep = 1.0 / 120.0;

    private const int MaxStepsPerAdvance = 12;
    private const double Gravity = 1800;            // px/s²
    private const int ConstraintIterations = 12;
    private const double Stiffness = 0.92;          // < 1 lets the rope stretch a little
    private const double OrnamentInverseMass = 1.0 / 3.0;
    private const double GrabFollow = 0.35;         // share of the gap closed per step
    private const double AngularSpring = 90;
    private const double RestSpeed = 4;             // px/s (≈0.07 px per frame, invisible)
    private const double RestAngularSpeed = 0.05;   // rad/s (≈2 px/s at the ornament rim)
    private const double SettleSpeed = 40;          // px/s: below this, settling friction fades in
    private const double SettleFriction = 8;        // extra damping per second when nearly still
    private const double RestHoldSeconds = 1;

    private readonly Vec2[] _pos = new Vec2[PointCount];
    private readonly Vec2[] _prev = new Vec2[PointCount];
    private Vec2 _anchor;
    private double _restLength;
    private double _swingStrength;
    private double _accumulator;
    private double _angle;
    private double _angularVelocity;
    private double _restTimer;
    private double _lastMaxSpeed;
    private Vec2? _grabTarget;
    private bool _frozen;

    public RopeSim(Vec2 anchor, double restLength, double swingStrength)
    {
        _anchor = anchor;
        _restLength = restLength;
        _swingStrength = Math.Clamp(swingStrength, 0, 1);
        LayOutStraight();
    }

    public Vec2 Anchor
    {
        get => _anchor;
        set
        {
            var shift = value - _anchor;
            _anchor = value;
            if (_frozen) LayOutStraight();
            else for (var i = 0; i < PointCount; i++) { _pos[i] += shift; _prev[i] += shift; }
            Wake();
        }
    }

    public double RestLength
    {
        get => _restLength;
        set
        {
            _restLength = value;
            if (_frozen) LayOutStraight();
            Wake();
        }
    }

    public double SwingStrength
    {
        get => _swingStrength;
        set => _swingStrength = Math.Clamp(value, 0, 1);
    }

    public IReadOnlyList<Vec2> Points => _pos;

    public Vec2 OrnamentPosition => _pos[^1];

    /// <summary>Radians; 0 = hanging straight down. Feed ×180/π to a WPF RotateTransform.</summary>
    public double OrnamentAngle => _angle;

    /// <summary>(path length / rest length) − 1.</summary>
    public double StretchRatio
    {
        get
        {
            double length = 0;
            for (var i = 0; i < PointCount - 1; i++) length += (_pos[i + 1] - _pos[i]).Length;
            return length / _restLength - 1;
        }
    }

    public bool IsAtRest { get; private set; }

    public void Grab(Vec2 target)
    {
        _grabTarget = target;
        Wake();
    }

    public void Release()
    {
        _grabTarget = null;
        Wake();
    }

    public void Freeze(bool frozen)
    {
        _frozen = frozen;
        if (frozen) LayOutStraight();
        Wake();
    }

    public void ApplyImpulse(Vec2 velocity)
    {
        _prev[^1] -= velocity * FixedStep;
        Wake();
    }

    public void Advance(double elapsedSeconds)
    {
        if (IsAtRest || elapsedSeconds <= 0) return;

        _accumulator += elapsedSeconds;
        var steps = 0;
        while (_accumulator >= FixedStep && steps < MaxStepsPerAdvance)
        {
            Step();
            _accumulator -= FixedStep;
            steps++;
        }
        // After a long gap (sleep/resume) drop the backlog instead of fast-forwarding.
        if (steps == MaxStepsPerAdvance) _accumulator = 0;
    }

    private void Step()
    {
        if (_frozen)
        {
            LayOutStraight();
            return;
        }

        const double dt = FixedStep;
        // Air drag keeps big swings lively; settling friction kills the last tiny wiggles.
        var settle = SettleFriction * Math.Clamp(1 - _lastMaxSpeed / SettleSpeed, 0, 1);
        var keep = 1 - (Lerp(2.6, 0.9, _swingStrength) + settle) * dt;
        var maxSpeed = 0.0;

        for (var i = 1; i < PointCount; i++)
        {
            var velocity = (_pos[i] - _prev[i]) * keep;
            _prev[i] = _pos[i];
            _pos[i] += velocity + new Vec2(0, Gravity * dt * dt);
        }

        if (_grabTarget is { } target)
            _pos[^1] += (target - _pos[^1]) * GrabFollow;

        SatisfyConstraints();

        for (var i = 1; i < PointCount; i++)
            maxSpeed = Math.Max(maxSpeed, (_pos[i] - _prev[i]).Length / dt);
        _lastMaxSpeed = maxSpeed;

        var last = _pos[^1] - _pos[^2];
        var targetAngle = -Math.Atan2(last.X, last.Y);
        var error = WrapAngle(targetAngle - _angle);
        _angularVelocity += (AngularSpring * error - (Lerp(16, 8, _swingStrength) + settle) * _angularVelocity) * dt;
        _angle = WrapAngle(_angle + _angularVelocity * dt);

        var still = maxSpeed < RestSpeed && Math.Abs(_angularVelocity) < RestAngularSpeed && _grabTarget is null;
        _restTimer = still ? _restTimer + dt : 0;
        if (_restTimer >= RestHoldSeconds) IsAtRest = true;
    }

    private void SatisfyConstraints()
    {
        var segment = _restLength / (PointCount - 1);
        for (var iteration = 0; iteration < ConstraintIterations; iteration++)
        {
            _pos[0] = _anchor;
            for (var i = 0; i < PointCount - 1; i++)
            {
                var wa = InverseMass(i);
                var wb = InverseMass(i + 1);
                var total = wa + wb;
                if (total <= 0) continue;

                var delta = _pos[i + 1] - _pos[i];
                var distance = delta.Length;
                if (distance < 1e-9) continue;

                var correction = delta * ((distance - segment) / distance * Stiffness);
                _pos[i] += correction * (wa / total);
                _pos[i + 1] -= correction * (wb / total);
            }
        }
    }

    private double InverseMass(int i)
    {
        if (i == 0) return 0;
        if (i == PointCount - 1) return _grabTarget is null ? OrnamentInverseMass : 0;
        return 1;
    }

    private void LayOutStraight()
    {
        var segment = _restLength / (PointCount - 1);
        for (var i = 0; i < PointCount; i++)
        {
            _pos[i] = _anchor + new Vec2(0, segment * i);
            _prev[i] = _pos[i];
        }
        _angle = 0;
        _angularVelocity = 0;
        _lastMaxSpeed = 0;
    }

    private void Wake()
    {
        IsAtRest = false;
        _restTimer = 0;
    }

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static double WrapAngle(double a) => Math.IEEERemainder(a, 2 * Math.PI);
}
