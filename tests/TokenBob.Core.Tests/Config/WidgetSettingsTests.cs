using TokenBob.Core.Config;

namespace TokenBob.Core.Tests.Config;

public class WidgetSettingsTests
{
    [Fact]
    public void Defaults_match_spec()
    {
        var s = new WidgetSettings();
        Assert.Equal(AppTheme.System, s.Theme);
        Assert.Equal(OrnamentStyle.Ring, s.Style);
        Assert.Equal(RopeStyle.TwistedCord, s.Rope);
        Assert.Equal(CordColor.Gold, s.Cord);
        Assert.Equal(CenterReadout.FiveHour, s.Center);
        Assert.Equal(150, s.RopeLength);
        Assert.Equal(1.0, s.OrnamentScale);
        Assert.Equal(1.0, s.Opacity);
        Assert.Equal(0.5, s.SwingStrength);
        Assert.True(s.Wind);
        Assert.True(s.HoverCard);
        Assert.True(s.ResetBlast);
        Assert.True(s.Shiver);
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
        var original = new WidgetSettings { RopeLength = 10, OrnamentScale = 3, SwingStrength = -1, Opacity = 0 };
        var n = original.Normalized();

        Assert.Equal(60, n.RopeLength);
        Assert.Equal(1.4, n.OrnamentScale);
        Assert.Equal(0, n.SwingStrength);
        Assert.Equal(WidgetSettings.MinOpacity, n.Opacity);
        Assert.Equal(10, original.RopeLength);
        Assert.Equal(0, original.Opacity);
    }

    [Fact]
    public void Opacity_above_1_is_clamped_to_fully_opaque()
    {
        Assert.Equal(1, new WidgetSettings { Opacity = 1.7 }.Normalized().Opacity);
    }

    [Fact]
    public void Unknown_choices_from_a_newer_or_edited_file_fall_back_to_defaults()
    {
        var s = new WidgetSettings
        {
            Theme = (AppTheme)9, Style = (OrnamentStyle)7, Rope = (RopeStyle)5, Cord = (CordColor)42, Center = (CenterReadout)3,
        }.Normalized();

        Assert.Equal(AppTheme.System, s.Theme);
        Assert.Equal(OrnamentStyle.Ring, s.Style);
        Assert.Equal(RopeStyle.TwistedCord, s.Rope);
        Assert.Equal(CordColor.Gold, s.Cord);
        Assert.Equal(CenterReadout.FiveHour, s.Center);
    }

    [Fact]
    public void Known_choices_are_kept()
    {
        var s = new WidgetSettings { Theme = AppTheme.Dark, Cord = CordColor.Crimson, Center = CenterReadout.Weekly }.Normalized();
        Assert.Equal(AppTheme.Dark, s.Theme);
        Assert.Equal(CordColor.Crimson, s.Cord);
        Assert.Equal(CenterReadout.Weekly, s.Center);
    }
}
