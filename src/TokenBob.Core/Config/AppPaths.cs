namespace TokenBob.Core.Config;

/// <summary>TokenBob's own folder: %AppData%\TokenBob.</summary>
public static class AppPaths
{
    public static string Root { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TokenBob");

    public static string SettingsFile { get; } = Path.Combine(Root, "settings.json");
    public static string AlertStateFile { get; } = Path.Combine(Root, "alert-state.json");
    public static string LogsDir { get; } = Path.Combine(Root, "logs");
    public static string WebView2Dir { get; } = Path.Combine(Root, "WebView2");
}
