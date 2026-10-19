using Microsoft.Extensions.Time.Testing;
using TokenBob.Core.Config;
using TokenBob.Core.Logging;
using TokenBob.Core.Tests.TestDoubles;

namespace TokenBob.Core.Tests.Logging;

public class FileLogTests
{
    [Fact]
    public void FileLog_writes_daily_file_and_prunes_files_older_than_7_days()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("tokenbob-20261001.log"), "old");
        File.WriteAllText(dir.File("tokenbob-20261010.log"), "recent");
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero));

        var log = new FileLog(dir.Path, time);
        log.Info("hello");
        log.Error("boom", new InvalidOperationException("bad"));

        Assert.False(File.Exists(dir.File("tokenbob-20261001.log")));
        Assert.True(File.Exists(dir.File("tokenbob-20261010.log")));
        var today = File.ReadAllText(dir.File("tokenbob-20261014.log"));
        Assert.Contains("INFO hello", today);
        Assert.Contains("ERROR boom", today);
        Assert.Contains("InvalidOperationException", today);
    }

    [Fact]
    public void AppPaths_live_under_appdata_TokenBob()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TokenBob");
        Assert.Equal(root, AppPaths.Root);
        Assert.Equal(Path.Combine(root, "settings.json"), AppPaths.SettingsFile);
        Assert.Equal(Path.Combine(root, "alert-state.json"), AppPaths.AlertStateFile);
        Assert.Equal(Path.Combine(root, "logs"), AppPaths.LogsDir);
        // The sign-in profile is the one thing that must not roam.
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TokenBob");
        Assert.Equal(Path.Combine(local, "WebView2"), AppPaths.WebView2Dir);
    }
}
