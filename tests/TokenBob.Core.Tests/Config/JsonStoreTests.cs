using TokenBob.Core.Config;
using TokenBob.Core.Logging;
using TokenBob.Core.Screens;
using TokenBob.Core.Tests.TestDoubles;

namespace TokenBob.Core.Tests.Config;

public class JsonStoreTests
{
    [Fact]
    public void Save_then_Load_round_trips()
    {
        using var dir = new TempDir();
        var store = new JsonStore<WidgetSettings>(dir.File("settings.json"), NullLog.Instance);
        var saved = new WidgetSettings
        {
            Theme = AppTheme.Dark,
            Style = OrnamentStyle.Tag,
            Rope = RopeStyle.BeadChain,
            Cord = CordColor.Crimson,
            Center = CenterReadout.Weekly,
            RopeLength = 222,
            OrnamentScale = 1.2,
            Opacity = 0.7,
            SwingStrength = 0.8,
            Wind = false,
            HoverCard = false,
            ResetBlast = false,
            Shiver = false,
            RefreshMinutes = 10,
            Notifications = false,
            StartWithWindows = true,
            AutoHideFullscreen = false,
            Monitor = new MonitorId(@"\\.\DISPLAY2", new ScreenRect(1920, 0, 2880, 1620)),
            AnchorXDevice = 3000,
        };

        store.Save(saved);
        var loaded = store.Load();

        Assert.Equal(AppTheme.Dark, loaded.Theme);
        Assert.Equal(OrnamentStyle.Tag, loaded.Style);
        Assert.Equal(RopeStyle.BeadChain, loaded.Rope);
        Assert.Equal(CordColor.Crimson, loaded.Cord);
        Assert.Equal(CenterReadout.Weekly, loaded.Center);
        Assert.Equal(0.7, loaded.Opacity);
        Assert.False(loaded.HoverCard);
        Assert.False(loaded.ResetBlast);
        Assert.False(loaded.Shiver);
        Assert.Equal(222, loaded.RopeLength);
        Assert.Equal(1.2, loaded.OrnamentScale);
        Assert.Equal(0.8, loaded.SwingStrength);
        Assert.False(loaded.Wind);
        Assert.Equal(10, loaded.RefreshMinutes);
        Assert.False(loaded.Notifications);
        Assert.True(loaded.StartWithWindows);
        Assert.False(loaded.AutoHideFullscreen);
        Assert.Equal(saved.Monitor, loaded.Monitor);
        Assert.Equal(3000, loaded.AnchorXDevice);
    }

    [Fact]
    public void Missing_file_gives_defaults()
    {
        using var dir = new TempDir();
        var loaded = new JsonStore<WidgetSettings>(dir.File("nope.json"), NullLog.Instance).Load();
        Assert.Equal(150, loaded.RopeLength);
        Assert.Equal(OrnamentStyle.Ring, loaded.Style);
    }

    [Fact]
    public void Corrupt_file_gives_defaults_and_keeps_bak()
    {
        using var dir = new TempDir();
        var path = dir.File("settings.json");
        File.WriteAllText(path, "{not json");

        var loaded = new JsonStore<WidgetSettings>(path, NullLog.Instance).Load();

        Assert.Equal(150, loaded.RopeLength);
        Assert.Equal("{not json", File.ReadAllText(path + ".bak"));
    }

    [Fact]
    public void Unknown_enum_value_keeps_other_settings()
    {
        using var dir = new TempDir();
        var path = dir.File("settings.json");
        File.WriteAllText(path, "{\"RopeLength\":222,\"RefreshMinutes\":10,\"Style\":\"Spark\"}");

        var loaded = new JsonStore<WidgetSettings>(path, NullLog.Instance).Load();

        Assert.Equal(222, loaded.RopeLength);
        Assert.Equal(10, loaded.RefreshMinutes);
        Assert.Equal(OrnamentStyle.Ring, loaded.Style);
        Assert.False(File.Exists(path + ".bak"));
    }

    [Fact]
    public void Wrong_value_types_keep_other_settings()
    {
        using var dir = new TempDir();
        var path = dir.File("settings.json");
        File.WriteAllText(path, "{\"RopeLength\":\"222\",\"SwingStrength\":null,\"OrnamentScale\":true,\"RefreshMinutes\":10}");

        var loaded = new JsonStore<WidgetSettings>(path, NullLog.Instance).Load();

        Assert.Equal(222, loaded.RopeLength);
        Assert.Equal(0.5, loaded.SwingStrength);
        Assert.Equal(1.0, loaded.OrnamentScale);
        Assert.Equal(10, loaded.RefreshMinutes);
    }

    [Fact]
    public void Locked_file_gives_defaults_without_throwing()
    {
        using var dir = new TempDir();
        var path = dir.File("settings.json");
        File.WriteAllText(path, "{\"RopeLength\":222}");
        using var lockHandle = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var store = new JsonStore<WidgetSettings>(path, NullLog.Instance);

        Assert.Equal(150, store.Load().RopeLength);
        Assert.False(store.Save(new WidgetSettings()));
    }

    [Fact]
    public void Load_clamps_out_of_range_values_and_ignores_unknown_fields()
    {
        using var dir = new TempDir();
        var path = dir.File("settings.json");
        File.WriteAllText(path, "{\"RopeLength\":5000,\"RefreshMinutes\":7,\"FutureThing\":true}");

        var loaded = new JsonStore<WidgetSettings>(path, NullLog.Instance).Load().Normalized();

        Assert.Equal(400, loaded.RopeLength);
        Assert.Equal(5, loaded.RefreshMinutes);
    }
}
