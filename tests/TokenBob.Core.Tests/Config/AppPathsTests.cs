using TokenBob.Core.Config;

namespace TokenBob.Core.Tests.Config;

public class AppPathsTests
{
    [Fact]
    public void Sign_in_profile_lives_in_the_local_not_the_roaming_folder()
    {
        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.StartsWith(local, AppPaths.WebView2Dir, StringComparison.Ordinal);
        Assert.StartsWith(roaming, AppPaths.LegacyWebView2Dir, StringComparison.Ordinal);
        Assert.NotEqual(AppPaths.WebView2Dir, AppPaths.LegacyWebView2Dir);
    }

    [Fact]
    public void Settings_alerts_and_logs_stay_together_under_the_root()
    {
        Assert.Equal(AppPaths.Root, Path.GetDirectoryName(AppPaths.SettingsFile));
        Assert.Equal(AppPaths.Root, Path.GetDirectoryName(AppPaths.AlertStateFile));
        Assert.Equal(AppPaths.Root, Path.GetDirectoryName(AppPaths.LogsDir));
    }
}
