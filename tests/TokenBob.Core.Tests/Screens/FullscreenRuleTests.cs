using TokenBob.Core.Screens;

namespace TokenBob.Core.Tests.Screens;

public class FullscreenRuleTests
{
    private static readonly ScreenRect Monitor = new(0, 0, 1920, 1080);

    private static ForegroundWindow Window(ScreenRect bounds, bool maximized = false, bool ignorable = false,
        bool onMonitor = true, int state = FullscreenRule.QunsAcceptsNotifications) =>
        new(bounds, maximized, ignorable, onMonitor, state);

    [Fact]
    public void Window_covering_the_monitor_is_fullscreen()
    {
        Assert.True(FullscreenRule.IsFullscreen(Window(new ScreenRect(0, 0, 1920, 1080)), Monitor));
    }

    [Fact]
    public void Maximized_window_is_not_fullscreen()
    {
        // On a monitor without a taskbar a maximized window covers it, with its borders past the edges.
        Assert.False(FullscreenRule.IsFullscreen(Window(new ScreenRect(-8, -8, 1936, 1096), maximized: true), Monitor));
    }

    [Fact]
    public void Maximized_browser_in_video_fullscreen_is_fullscreen()
    {
        // YouTube fullscreen in a maximized browser: still flagged maximized, but exactly fills the monitor.
        Assert.True(FullscreenRule.IsFullscreen(Window(new ScreenRect(0, 0, 1920, 1080), maximized: true), Monitor));
    }

    [Fact]
    public void Busy_state_on_this_monitor_is_fullscreen()
    {
        Assert.True(FullscreenRule.IsFullscreen(Window(new ScreenRect(-8, -8, 1936, 1096), maximized: true, state: FullscreenRule.QunsBusy), Monitor));
    }

    [Fact]
    public void Smaller_window_is_not_fullscreen()
    {
        Assert.False(FullscreenRule.IsFullscreen(Window(new ScreenRect(100, 100, 1200, 800)), Monitor));
    }

    [Fact]
    public void Desktop_or_own_window_is_not_fullscreen()
    {
        Assert.False(FullscreenRule.IsFullscreen(Window(new ScreenRect(0, 0, 1920, 1080), ignorable: true), Monitor));
    }

    [Fact]
    public void Fullscreen_on_another_monitor_does_not_count()
    {
        Assert.False(FullscreenRule.IsFullscreen(Window(new ScreenRect(1920, 0, 1920, 1080), onMonitor: false), Monitor));
    }

    [Theory]
    [InlineData(FullscreenRule.QunsRunningD3DFullScreen)]
    [InlineData(FullscreenRule.QunsPresentationMode)]
    public void Exclusive_game_or_presentation_on_this_monitor_is_fullscreen(int state)
    {
        Assert.True(FullscreenRule.IsFullscreen(Window(new ScreenRect(200, 200, 800, 600), state: state), Monitor));
    }
}
