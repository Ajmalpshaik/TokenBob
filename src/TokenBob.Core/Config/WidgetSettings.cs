using TokenBob.Core.Screens;

namespace TokenBob.Core.Config;

public enum OrnamentStyle { Ring, Tag }

public enum RopeStyle { TwistedCord, BeadChain }

/// <summary>Everything the user can change in the Settings window, plus where the widget hangs.</summary>
public sealed class WidgetSettings
{
    public static readonly int[] RefreshChoices = [1, 2, 5, 10, 15];

    public OrnamentStyle Style { get; set; } = OrnamentStyle.Ring;
    public RopeStyle Rope { get; set; } = RopeStyle.TwistedCord;
    public double RopeLength { get; set; } = 150;
    public double OrnamentScale { get; set; } = 1.0;
    public double SwingStrength { get; set; } = 0.5;
    public bool Wind { get; set; } = true;
    public int RefreshMinutes { get; set; } = 5;
    public bool Notifications { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool AutoHideFullscreen { get; set; } = true;
    public MonitorId? Monitor { get; set; }
    public double? AnchorXDevice { get; set; }

    /// <summary>A copy with every value pulled into its allowed range.</summary>
    public WidgetSettings Normalized()
    {
        var copy = (WidgetSettings)MemberwiseClone();
        copy.RopeLength = Math.Clamp(RopeLength, Placement.MinRope, Placement.MaxRope);
        copy.OrnamentScale = Math.Clamp(OrnamentScale, 0.8, 1.4);
        copy.SwingStrength = Math.Clamp(SwingStrength, 0, 1);
        copy.RefreshMinutes = RefreshChoices.MinBy(choice => Math.Abs(choice - RefreshMinutes));
        return copy;
    }
}
