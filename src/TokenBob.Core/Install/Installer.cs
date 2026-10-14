namespace TokenBob.Core.Install;

public enum LaunchMode { Run, Install, Update, Uninstall }

/// <summary>Where an installed TokenBob lives. Per user, so no admin rights are needed.</summary>
public sealed record InstallPaths(string Dir, string Exe, string StartMenuShortcut)
{
    public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + AppInfo.Name;

    public static InstallPaths For(string localAppData, string appData)
    {
        var dir = Path.Combine(localAppData, "Programs", AppInfo.Name);
        return new InstallPaths(dir, Path.Combine(dir, AppInfo.Name + ".exe"),
            Path.Combine(appData, "Microsoft", "Windows", "Start Menu", "Programs", AppInfo.Name + ".lnk"));
    }
}

/// <summary>Decides what a double-click on TokenBob.exe should do.</summary>
public static class Installer
{
    private static readonly string[] RunOnlyOptions = ["--no-install", "--render-samples", "--demo-alerts"];

    public static LaunchMode Decide(IReadOnlyList<string> args, string exePath, InstallPaths paths,
        bool installedExists, bool singleFileBuild)
    {
        if (args.Contains("--uninstall")) return LaunchMode.Uninstall;
        if (!singleFileBuild || args.Any(RunOnlyOptions.Contains)) return LaunchMode.Run;
        if (string.Equals(Path.GetFullPath(exePath), Path.GetFullPath(paths.Exe), StringComparison.OrdinalIgnoreCase))
            return LaunchMode.Run;
        return installedExists ? LaunchMode.Update : LaunchMode.Install;
    }

    /// <summary>
    /// Whether this copy may keep the "start with Windows" entry pointing at itself. Only the
    /// installed copy does, so a developer or test run never takes it over.
    /// </summary>
    public static bool OwnsStartupEntry(string exePath, InstallPaths paths) =>
        string.Equals(Path.GetFullPath(exePath), Path.GetFullPath(paths.Exe), StringComparison.OrdinalIgnoreCase);
}
