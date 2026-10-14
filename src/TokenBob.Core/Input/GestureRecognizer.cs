using TokenBob.Core.Physics;

namespace TokenBob.Core.Input;

[Flags]
public enum Modifiers { None = 0, Ctrl = 1, Shift = 2 }

public enum GestureEventKind { OpenSettings, OpenUsagePage, DragStart, DragMove, DragEnd, EditStart, EditMove, EditEnd }

public readonly record struct GestureEvent(GestureEventKind Kind, Vec2 Position);

/// <summary>
/// Turns raw pointer input on the ornament into gestures. Modifier keys are read only
/// when the button goes down, so letting go of Shift mid-drag keeps editing.
/// </summary>
public sealed class GestureRecognizer
{
    public const double DragThreshold = 4;
    private const double DoubleClickSlop = 8;

    private enum Mode { Idle, Swallow, PendingDrag, Drag, PendingEdit, Edit }

    private readonly TimeSpan _doubleClickTime;
    private Mode _mode = Mode.Idle;
    private Vec2 _downAt;
    private (Vec2 At, TimeSpan Time)? _lastClick;

    public GestureRecognizer(TimeSpan doubleClickTime) => _doubleClickTime = doubleClickTime;

    public IReadOnlyList<GestureEvent> Down(Vec2 p, Modifiers mods, TimeSpan t)
    {
        _downAt = p;
        if (mods.HasFlag(Modifiers.Ctrl))
        {
            _mode = Mode.Swallow;
            _lastClick = null;
            return [new GestureEvent(GestureEventKind.OpenSettings, p)];
        }
        _mode = mods.HasFlag(Modifiers.Shift) ? Mode.PendingEdit : Mode.PendingDrag;
        return [];
    }

    public IReadOnlyList<GestureEvent> Move(Vec2 p, TimeSpan t)
    {
        switch (_mode)
        {
            case Mode.PendingDrag when Moved(p):
                _mode = Mode.Drag;
                _lastClick = null;
                return [new GestureEvent(GestureEventKind.DragStart, _downAt), new GestureEvent(GestureEventKind.DragMove, p)];
            case Mode.PendingEdit when Moved(p):
                _mode = Mode.Edit;
                _lastClick = null;
                return [new GestureEvent(GestureEventKind.EditStart, _downAt), new GestureEvent(GestureEventKind.EditMove, p)];
            case Mode.Drag:
                return [new GestureEvent(GestureEventKind.DragMove, p)];
            case Mode.Edit:
                return [new GestureEvent(GestureEventKind.EditMove, p)];
            default:
                return [];
        }
    }

    public IReadOnlyList<GestureEvent> Up(Vec2 p, TimeSpan t)
    {
        var mode = _mode;
        _mode = Mode.Idle;
        switch (mode)
        {
            case Mode.Drag:
                return [new GestureEvent(GestureEventKind.DragEnd, p)];
            case Mode.Edit:
                return [new GestureEvent(GestureEventKind.EditEnd, p)];
            case Mode.PendingDrag:
                if (_lastClick is { } last && t - last.Time <= _doubleClickTime && (p - last.At).Length <= DoubleClickSlop)
                {
                    _lastClick = null;
                    return [new GestureEvent(GestureEventKind.OpenUsagePage, p)];
                }
                _lastClick = (p, t);
                return [];
            default:
                return [];
        }
    }

    private bool Moved(Vec2 p) => (p - _downAt).Length >= DragThreshold;
}
