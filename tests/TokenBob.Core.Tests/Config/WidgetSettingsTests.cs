using TokenBob.Core.Config;

namespace TokenBob.Core.Tests.Config;

public class WidgetSettingsTests
{
    [Fact]
    public void Defaults_match_spec()
    {
        var s = new WidgetSettings();
        Assert.Equal(OrnamentStyle.Ring, s.Style);
        Assert.Equal(RopeStyle.TwistedCord, s.Rope);
        Assert.Equal(150, s.RopeLength);
        Assert.Equal(1.0, s.OrnamentScale);
        Assert.Equal(0.5, s.SwingStrength);
        Assert.True(s.Wind);
        Assert.Equal(5, s.RefreshMinutes);
        Assert.True(s.Notifications);
        Assert.False(s.StartWithWindows);
        Assert.True(s.AutoHideFullscreen);
        Assert.Null(s.Monitor);
        Assert.Null(s.AnchorXDevice);
    }

    [Theory]
    [InlineData(13, 15)]
    [InlineData(0, 1)]
    [InlineData(7, 5)]
    [InlineData(99, 15)]
    [InlineData(2, 2)]
    public void RefreshMinutes_snaps_to_nearest_choice(int given, int expected)
    {
        Assert.Equal(expected, new WidgetSettings { RefreshMinutes = given }.Normalized().RefreshMinutes);
    }

    [Fact]
    public void Normalized_clamps_ranges_without_changing_the_original()
    {
        var original = new WidgetSettings { RopeLength = 10, OrnamentScale = 3, SwingStrength = -1 };
        var n = original.Normalized();

        Assert.Equal(60, n.RopeLength);
        Assert.Equal(1.4, n.OrnamentScale);
        Assert.Equal(0, n.SwingStrength);
        Assert.Equal(10, original.RopeLength);
    }
}
