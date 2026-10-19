namespace TokenBob.Core.Config;

/// <summary>
/// TokenBob's own folders. Settings, alert state and logs are small and live in
/// %AppData%\TokenBob (a roaming profile may carry them along). The claude.ai sign-in
/// profile holds the session cookie, so it lives in %LocalAppData%\TokenBob and stays
/// on this PC.
/// </summary>
public static class AppPaths
{
    public static string Root { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TokenBob");

    public static string LocalRoot { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TokenBob");

    public static string SettingsFile { get; } = Path.Combine(Root, "settings.json");
    public static string AlertStateFile { get; } = Path.Combine(Root, "alert-state.json");
    public static string LogsDir { get; } = Path.Combine(Root, "logs");
    public static string WebView2Dir { get; } = Path.Combine(LocalRoot, "WebView2");

    /// <summary>Where versions before 0.2 kept the sign-in profile (moved once at start-up).</summary>
    public static string LegacyWebView2Dir { get; } = Path.Combine(Root, "WebView2");
}
