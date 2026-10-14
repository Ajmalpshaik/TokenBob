using Microsoft.Win32;
using TokenBob.Core;
using TokenBob.Core.Config;

namespace TokenBob.App.Platform;

/// <summary>
/// "Start with Windows": a value under the current user's Run key plus Windows' matching
/// "approved" value (what the Startup apps page shows). No admin rights needed.
/// </summary>
internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    /// <summary>On, and not switched off in Task Manager or Settings → Apps → Startup.</summary>
    public static bool IsEnabled()
    {
        using var run = Registry.CurrentUser.OpenSubKey(RunKey);
        if (run?.GetValue(AppInfo.Name) is not string) return false;
        return StartupApproval.Read(ReadApproval()) != StartupApprovalState.Disabled;
    }

    /// <summary>
    /// The user ticked or unticked "Start with Windows" (in Settings or the installer).
    /// <paramref name="exePath"/> defaults to this exe; the installer passes the installed copy.
    /// </summary>
    public static void Set(bool enabled, string? exePath = null)
    {
        using var run = Registry.CurrentUser.CreateSubKey(RunKey);
        using var approved = Registry.CurrentUser.CreateSubKey(ApprovedKey);
        if (enabled && (exePath ?? Environment.ProcessPath) is { } exe)
        {
            run.SetValue(AppInfo.Name, $"\"{exe}\"");
            approved.SetValue(AppInfo.Name, StartupApproval.EnabledValue(), RegistryValueKind.Binary);
        }
        else
        {
            run.DeleteValue(AppInfo.Name, throwOnMissingValue: false);
            approved.DeleteValue(AppInfo.Name, throwOnMissingValue: false);
        }
    }

    /// <summary>
    /// At launch: keep the entry pointing at this exe (in case it moved) and add the approved
    /// value if Windows never got one — but never undo the user switching it off in Windows.
    /// </summary>
    public static void Refresh()
    {
        using var run = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (run?.GetValue(AppInfo.Name) is not string || Environment.ProcessPath is not { } exe) return;

        run.SetValue(AppInfo.Name, $"\"{exe}\"");
        if (StartupApproval.Read(ReadApproval()) == StartupApprovalState.Missing)
        {
            using var approved = Registry.CurrentUser.CreateSubKey(ApprovedKey);
            approved.SetValue(AppInfo.Name, StartupApproval.EnabledValue(), RegistryValueKind.Binary);
        }
    }

    private static byte[]? ReadApproval()
    {
        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey);
        return approved?.GetValue(AppInfo.Name) as byte[];
    }
}
