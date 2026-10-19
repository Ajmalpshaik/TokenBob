using TokenBob.Core.Input;
using TokenBob.Core.Physics;

namespace TokenBob.Core.Tests.Input;

public class GestureRecognizerTests
{
    private static readonly TimeSpan DoubleClick = TimeSpan.FromMilliseconds(500);
    private static TimeSpan Ms(int ms) => TimeSpan.FromMilliseconds(ms);
    private static GestureEventKind[] Kinds(IEnumerable<GestureEvent> events) => events.Select(e => e.Kind).ToArray();

    [Fact]
    public void Ctrl_down_opens_settings_and_swallows_rest()
    {
        var g = new GestureRecognizer(DoubleClick);
        Assert.Equal([GestureEventKind.OpenSettings], Kinds(g.Down(new Vec2(10, 10), Modifiers.Ctrl, Ms(0))));
        Assert.Empty(g.Move(new Vec2(60, 60), Ms(10)));
        Assert.Empty(g.Up(new Vec2(60, 60), Ms(20)));
    }

    [Fact]
    public void Shift_drag_is_edit_mode()
    {
        var g = new GestureRecognizer(DoubleClick);
        Assert.Empty(g.Down(new Vec2(10, 10), Modifiers.Shift, Ms(0)));
        Assert.Equal([GestureEventKind.EditStart, GestureEventKind.EditMove], Kinds(g.Move(new Vec2(20, 10), Ms(10))));
        Assert.Equal([GestureEventKind.EditEnd], Kinds(g.Up(new Vec2(20, 10), Ms(20))));
    }

    [Fact]
    public void Releasing_shift_mid_drag_stays_in_edit()
    {
        var g = new GestureRecognizer(DoubleClick);
        g.Down(new Vec2(10, 10), Modifiers.Shift, Ms(0));
        g.Move(new Vec2(20, 10), Ms(10));
        // The recognizer only reads modifiers on Down, so later moves stay edits.
        Assert.Equal([GestureEventKind.EditMove], Kinds(g.Move(new Vec2(40, 10), Ms(20))));
        Assert.Equal([GestureEventKind.EditEnd], Kinds(g.Up(new Vec2(40, 10), Ms(30))));
    }

    [Fact]
    public void Plain_drag_past_threshold_is_drag()
    {
        var g = new GestureRecognizer(DoubleClick);
        g.Down(new Vec2(10, 10), Modifiers.None, Ms(0));
        Assert.Empty(g.Move(new Vec2(13, 10), Ms(10)));
        Assert.Equal([GestureEventKind.DragStart, GestureEventKind.DragMove], Kinds(g.Move(new Vec2(15, 10), Ms(20))));
        Assert.Equal([GestureEventKind.DragMove], Kinds(g.Move(new Vec2(30, 10), Ms(30))));
        Assert.Equal([GestureEventKind.DragEnd], Kinds(g.Up(new Vec2(30, 10), Ms(40))));
    }

    [Fact]
    public void Two_clicks_within_double_click_time_open_usage_page()
    {
        var g = new GestureRecognizer(DoubleClick);
        g.Down(new Vec2(10, 10), Modifiers.None, Ms(0));
        Assert.Empty(g.Up(new Vec2(10, 10), Ms(80)));
        g.Down(new Vec2(11, 10), Modifiers.None, Ms(250));
        Assert.Equal([GestureEventKind.OpenUsagePage], Kinds(g.Up(new Vec2(11, 10), Ms(300))));
    }

    [Fact]
    public void Two_slow_clicks_do_not()
    {
        var g = new GestureRecognizer(DoubleClick);
        g.Down(new Vec2(10, 10), Modifiers.None, Ms(0));
        g.Up(new Vec2(10, 10), Ms(80));
        g.Down(new Vec2(10, 10), Modifiers.None, Ms(800));
        Assert.Empty(g.Up(new Vec2(10, 10), Ms(880)));
    }

    [Fact]
    public void A_drag_between_clicks_breaks_the_double_click()
    {
        var g = new GestureRecognizer(DoubleClick);
        g.Down(new Vec2(10, 10), Modifiers.None, Ms(0));
        g.Up(new Vec2(10, 10), Ms(50));
        g.Down(new Vec2(10, 10), Modifiers.None, Ms(100));
        g.Move(new Vec2(40, 10), Ms(150));
        Assert.Equal([GestureEventKind.DragEnd], Kinds(g.Up(new Vec2(40, 10), Ms(200))));
    }
    [Fact]
    public void Cancel_ends_an_open_drag_and_a_later_click_is_a_plain_click()
    {
        var g = new GestureRecognizer(DoubleClick);
        g.Down(new Vec2(10, 10), Modifiers.None, Ms(0));
        g.Move(new Vec2(30, 10), Ms(10));

        Assert.Equal([GestureEventKind.DragEnd], Kinds(g.Cancel(new Vec2(30, 10))));
        Assert.Empty(g.Cancel(new Vec2(30, 10)));
        Assert.Empty(g.Up(new Vec2(30, 10), Ms(20)));

        g.Down(new Vec2(10, 10), Modifiers.None, Ms(100));
        Assert.Empty(g.Up(new Vec2(10, 10), Ms(120)));
    }

    [Fact]
    public void Cancel_ends_an_open_edit()
    {
        var g = new GestureRecognizer(DoubleClick);
        g.Down(new Vec2(10, 10), Modifiers.Shift, Ms(0));
        g.Move(new Vec2(30, 10), Ms(10));
        Assert.Equal([GestureEventKind.EditEnd], Kinds(g.Cancel(new Vec2(30, 10))));
    }

    [Fact]
    public void A_new_press_during_an_unreleased_drag_ends_the_drag_first()
    {
        var g = new GestureRecognizer(DoubleClick);
        g.Down(new Vec2(10, 10), Modifiers.None, Ms(0));
        g.Move(new Vec2(30, 10), Ms(10));

        Assert.Equal([GestureEventKind.DragEnd], Kinds(g.Down(new Vec2(50, 50), Modifiers.None, Ms(500))));
        Assert.Equal([GestureEventKind.DragStart, GestureEventKind.DragMove], Kinds(g.Move(new Vec2(60, 50), Ms(510))));
    }

    [Fact]
    public void Ctrl_press_during_an_unreleased_edit_ends_the_edit_then_opens_settings()
    {
        var g = new GestureRecognizer(DoubleClick);
        g.Down(new Vec2(10, 10), Modifiers.Shift, Ms(0));
        g.Move(new Vec2(30, 10), Ms(10));
        Assert.Equal([GestureEventKind.EditEnd, GestureEventKind.OpenSettings], Kinds(g.Down(new Vec2(30, 10), Modifiers.Ctrl, Ms(500))));
    }
}
