using TokenBob.Core.Screens;
using static TokenBob.App.Platform.NativeMethods;

namespace TokenBob.App.Platform;

internal static class Monitors
{
    /// <summary>All connected monitors with device-pixel bounds and DPI scale.</summary>
    public static IReadOnlyList<MonitorInfo> GetAll()
    {
        var result = new List<MonitorInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr handle, IntPtr _, ref RECT _, IntPtr _) =>
        {
            var info = new MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>() };
            if (!GetMonitorInfo(handle, ref info)) return true;

            var scale = GetDpiForMonitor(handle, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 ? dpiX / 96.0 : 1.0;
            var r = info.rcMonitor;
            result.Add(new MonitorInfo(
                info.szDevice,
                new ScreenRect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top),
                scale,
                (info.dwFlags & MONITORINFOF_PRIMARY) != 0));
            return true;
        }, IntPtr.Zero);
        return result;
    }
}
