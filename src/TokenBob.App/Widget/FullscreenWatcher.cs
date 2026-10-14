using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using TokenBob.Core.Screens;
using static TokenBob.App.Platform.NativeMethods;

namespace TokenBob.App.Widget;

/// <summary>Every 2 seconds, checks whether a fullscreen app covers the widget's monitor.</summary>
internal sealed class FullscreenWatcher
{
    private static readonly string[] ShellClasses = ["Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd"];

    private readonly Func<MonitorInfo> _monitor;
    private readonly DispatcherTimer _timer;
    private readonly int _ownProcessId = Environment.ProcessId;
    private bool _fullscreen;

    public FullscreenWatcher(Func<MonitorInfo> monitor, Dispatcher dispatcher)
    {
        _monitor = monitor;
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) => Check(), dispatcher);
    }

    public event Action<bool>? FullscreenChanged;

    public bool Enabled
    {
        get => _timer.IsEnabled;
        set
        {
            _timer.IsEnabled = value;
            if (!value) Report(false);
        }
    }

    private void Check() => Report(IsFullscreenNow());

    private void Report(bool fullscreen)
    {
        if (fullscreen == _fullscreen) return;
        _fullscreen = fullscreen;
        FullscreenChanged?.Invoke(fullscreen);
    }

    private bool IsFullscreenNow()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var r)) return false;

        GetWindowThreadProcessId(hwnd, out var pid);
        var className = new StringBuilder(64);
        GetClassName(hwnd, className, className.Capacity);
        var ignorable = pid == _ownProcessId || ShellClasses.Contains(className.ToString());

        var monitor = _monitor();
        var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        var onMonitor = GetMonitorInfo(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref info)
            && info.szDevice == monitor.DeviceName;

        SHQueryUserNotificationState(out var state);
        var window = new ForegroundWindow(new ScreenRect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top),
            IsZoomed(hwnd), ignorable, onMonitor, state);
        return FullscreenRule.IsFullscreen(window, monitor.BoundsDevice);
    }
}
