using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;

namespace TokenBob.App.Platform;

/// <summary>Hands unused memory back to Windows while the widget is idle.</summary>
internal static class MemoryTrim
{
    private static readonly TimeSpan MinGap = TimeSpan.FromSeconds(30);
    private static long _lastTick = -1;

    public static void Trim()
    {
        var now = Environment.TickCount64;
        if (_lastTick >= 0 && now - _lastTick < MinGap.TotalMilliseconds) return;
        _lastTick = now;

        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        using var self = Process.GetCurrentProcess();
        SetProcessWorkingSetSize(self.Handle, -1, -1);
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, nint minimum, nint maximum);
}
