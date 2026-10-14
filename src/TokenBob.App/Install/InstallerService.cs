using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using TokenBob.App.Platform;
using TokenBob.Core;
using TokenBob.Core.Config;
using TokenBob.Core.Install;

namespace TokenBob.App.Install;

/// <summary>Installs, updates and removes TokenBob for the current user. No admin rights.</summary>
internal static class InstallerService
{
    /// <summary>A running TokenBob quits when this event is set (used before updating or uninstalling).</summary>
    public const string QuitEventName = @"Local\TokenBob.Quit";

    public static InstallPaths Paths { get; } = InstallPaths.For(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

    public static bool IsInstalled => File.Exists(Paths.Exe);

    /// <summary>True for the published single-file exe; developer builds have TokenBob.dll beside the exe.</summary>
    public static bool IsSingleFileBuild => !File.Exists(Path.Combine(AppContext.BaseDirectory, AppInfo.Name + ".dll"));

    public static string Version => typeof(InstallerService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    public static void Install(bool startWithWindows)
    {
        var paths = Paths;
        QuitRunningCopies();

        Directory.CreateDirectory(paths.Dir);
        CopyWithRetry(Environment.ProcessPath!, paths.Exe);
        CreateShortcut(paths.StartMenuShortcut, paths.Exe, paths.Dir);
        RegisterUninstall(paths);
        StartupRegistration.Set(startWithWindows, paths.Exe);

        Process.Start(new ProcessStartInfo(paths.Exe) { UseShellExecute = true, WorkingDirectory = paths.Dir });
    }

    public static void Uninstall(bool removeData)
    {
        var paths = Paths;
        QuitRunningCopies();

        StartupRegistration.Set(false);
        TryDelete(paths.StartMenuShortcut);
        Registry.CurrentUser.DeleteSubKeyTree(InstallPaths.UninstallKey, throwOnMissingSubKey: false);

        // This exe is running from the install folder, so a short-lived helper removes it after we exit.
        var folders = removeData ? $"\"{paths.Dir}\" \"{AppPaths.Root}\"" : $"\"{paths.Dir}\"";
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c ping 127.0.0.1 -n 4 > nul & rmdir /s /q {folders}")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden,
        });
    }

    /// <summary>Asks any other running TokenBob to quit, and closes it after 5 s if it doesn't.</summary>
    public static void QuitRunningCopies()
    {
        try
        {
            using var quit = EventWaitHandle.OpenExisting(QuitEventName);
            quit.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // None running.
        }

        foreach (var other in Process.GetProcessesByName(AppInfo.Name).Where(p => p.Id != Environment.ProcessId))
        {
            using (other)
            {
                if (!other.WaitForExit(5000)) other.Kill();
                other.WaitForExit(2000);
            }
        }
    }

    private static void CopyWithRetry(string from, string to)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Copy(from, to, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 10)
            {
                Thread.Sleep(300);   // the old copy may still be closing
            }
        }
    }

    private static void CreateShortcut(string shortcut, string target, string workingDir)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcut)!);
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("WScript.Shell is not available.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic link = shell.CreateShortcut(shortcut);
        link.TargetPath = target;
        link.WorkingDirectory = workingDir;
        link.IconLocation = target + ",0";
        link.Description = "Hanging Claude usage meter";
        link.Save();
    }

    private static void RegisterUninstall(InstallPaths paths)
    {
        using var key = Registry.CurrentUser.CreateSubKey(InstallPaths.UninstallKey);
        key.SetValue("DisplayName", AppInfo.Name);
        key.SetValue("DisplayVersion", Version);
        key.SetValue("Publisher", "Ajmalpshaik");
        key.SetValue("DisplayIcon", paths.Exe + ",0");
        key.SetValue("InstallLocation", paths.Dir);
        key.SetValue("UninstallString", $"\"{paths.Exe}\" --uninstall");
        key.SetValue("URLInfoAbout", AppInfo.RepoUrl);
        key.SetValue("HelpLink", AppInfo.RepoUrl);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        key.SetValue("EstimatedSize", (int)(new FileInfo(paths.Exe).Length / 1024), RegistryValueKind.DWord);
    }

    private static void TryDelete(string file)
    {
        try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
