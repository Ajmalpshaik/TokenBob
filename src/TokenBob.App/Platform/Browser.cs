using System.Diagnostics;

namespace TokenBob.App.Platform;

internal static class Browser
{
    /// <summary>
    /// Opens a URL in the user's default browser, in front. The widget never takes focus,
    /// so it hands its right to bring a window forward (from the click) to the browser.
    /// </summary>
    public static void Open(string url)
    {
        NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
