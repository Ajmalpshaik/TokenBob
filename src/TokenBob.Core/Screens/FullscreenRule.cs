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
        if (window.NotificationState is QunsRunningD3DFullScreen or QunsPresentationMode) return true;
        if (window.Maximized) return false;

        var b = window.BoundsDevice;
        return b.Left <= monitorDevice.Left && b.Top <= monitorDevice.Top
            && b.Right >= monitorDevice.Right && b.Bottom >= monitorDevice.Bottom;
    }
}
