namespace TokenBob.Core.Screens;

/// <summary>What TokenBob knows about the window in front.</summary>
/// <param name="BoundsDevice">Its bounds in device pixels.</param>
/// <param name="Maximized">A normal maximized window — never treated as fullscreen.</param>
/// <param name="Ignorable">The desktop, the taskbar, or one of TokenBob's own windows.</param>
/// <param name="OnMonitor">It is on the widget's monitor.</param>
/// <param name="NotificationState">The value from SHQueryUserNotificationState.</param>
public sealed record ForegroundWindow(ScreenRect BoundsDevice, bool Maximized, bool Ignorable, bool OnMonitor, int NotificationState);

/// <summary>Decides whether a fullscreen app is covering the widget's monitor.</summary>
public static class FullscreenRule
{
    public const int QunsBusy = 2;
    public const int QunsRunningD3DFullScreen = 3;
    public const int QunsPresentationMode = 4;
    public const int QunsAcceptsNotifications = 5;

    public static bool IsFullscreen(ForegroundWindow window, ScreenRect monitorDevice)
    {
        if (window.Ignorable || !window.OnMonitor) return false;
        if (window.NotificationState is QunsBusy or QunsRunningD3DFullScreen or QunsPresentationMode) return true;

        var b = window.BoundsDevice;
        // A browser showing a fullscreen video can still be flagged maximized, but it fills the
        // monitor exactly; a real maximized window sticks out past the edges by its borders.
        if (ExactlyFills(b, monitorDevice)) return true;
        if (window.Maximized) return false;

        return b.Left <= monitorDevice.Left && b.Top <= monitorDevice.Top
            && b.Right >= monitorDevice.Right && b.Bottom >= monitorDevice.Bottom;
    }

    private static bool ExactlyFills(ScreenRect window, ScreenRect monitor) =>
        Math.Abs(window.Left - monitor.Left) <= 1 && Math.Abs(window.Top - monitor.Top) <= 1
        && Math.Abs(window.Right - monitor.Right) <= 1 && Math.Abs(window.Bottom - monitor.Bottom) <= 1;
}
