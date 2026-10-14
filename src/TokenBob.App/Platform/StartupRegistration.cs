using Microsoft.Win32;
using TokenBob.Core;

namespace TokenBob.App.Platform;

/// <summary>"Start with Windows": a value under the current user's Run key. No admin rights needed.</summary>
internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(AppInfo.Name) is string;
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled && Environment.ProcessPath is { } exe)
            key.SetValue(AppInfo.Name, $"\"{exe}\"");
        else
            key.DeleteValue(AppInfo.Name, throwOnMissingValue: false);
    }
}
