using TokenBob.Core.Screens;

namespace TokenBob.Core.Config;

public enum OrnamentStyle { Ring, Tag }

public enum RopeStyle { TwistedCord, BeadChain }

/// <summary>Light or dark for the widget and every window: follow Windows, or pick one.</summary>
public enum AppTheme { System, Light, Dark }

/// <summary>The colour of the twisted cord (the bead chain is always steel).</summary>
public enum CordColor { Gold, Graphite, Crimson, Ivory }

/// <summary>Which limit the ring shows in its middle.</summary>
public enum CenterReadout { FiveHour, Weekly }

/// <summary>Everything the user can change in the Settings window, plus where the widget hangs.</summary>
public sealed class WidgetSettings
{
    public static readonly int[] RefreshChoices = [1, 2, 5, 10, 15];
    public const double MinOpacity = 0.4;

    public AppTheme Theme { get; set; } = AppTheme.System;
    public OrnamentStyle Style { get; set; } = OrnamentStyle.Ring;
    public RopeStyle Rope { get; set; } = RopeStyle.TwistedCord;
    public CordColor Cord { get; set; } = CordColor.Gold;
    public CenterReadout Center { get; set; } = CenterReadout.FiveHour;
    public double RopeLength { get; set; } = 150;
    public double OrnamentScale { get; set; } = 1.0;
    public double Opacity { get; set; } = 1.0;
    public double SwingStrength { get; set; } = 0.5;
    public bool Wind { get; set; } = true;
    public bool HoverCard { get; set; } = true;
    public bool ResetBlast { get; set; } = true;
    public bool Shiver { get; set; } = true;
    public int RefreshMinutes { get; set; } = 5;
    public bool Notifications { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool AutoHideFullscreen { get; set; } = true;
    public MonitorId? Monitor { get; set; }
    public double? AnchorXDevice { get; set; }

    /// <summary>A copy with every value pulled into its allowed range (an unknown choice becomes the default).</summary>
    public WidgetSettings Normalized()
    {
        var copy = (WidgetSettings)MemberwiseClone();
        copy.Theme = Defined(Theme, AppTheme.System);
        copy.Style = Defined(Style, OrnamentStyle.Ring);
        copy.Rope = Defined(Rope, RopeStyle.TwistedCord);
        copy.Cord = Defined(Cord, CordColor.Gold);
        copy.Center = Defined(Center, CenterReadout.FiveHour);
        copy.RopeLength = Math.Clamp(RopeLength, Placement.MinRope, Placement.MaxRope);
        copy.OrnamentScale = Math.Clamp(OrnamentScale, 0.8, 1.4);
        copy.Opacity = Math.Clamp(Opacity, MinOpacity, 1);
        copy.SwingStrength = Math.Clamp(SwingStrength, 0, 1);
        copy.RefreshMinutes = RefreshChoices.MinBy(choice => Math.Abs(choice - RefreshMinutes));
        return copy;
    }

    private static T Defined<T>(T value, T fallback) where T : struct, Enum => Enum.IsDefined(value) ? value : fallback;
}
