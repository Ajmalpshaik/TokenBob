using System.Windows.Media;
using TokenBob.Core.Alerts;

namespace TokenBob.App.Widget;

/// <summary>All widget colors in one place. Card, track and text follow the Windows light/dark theme.</summary>
internal static class Palette
{
    public static readonly Brush Green = Frozen("#3FA45B");
    public static readonly Brush Amber = Frozen("#E8A23A");
    public static readonly Brush Red = Frozen("#E0533D");
    public static readonly Brush Grey = Frozen("#9A988F");

    private static readonly Brush TrackLight = Frozen("#E4E2DA"), TrackDark = Frozen("#3A3A38");
    private static readonly Brush CardLight = Frozen("#FFFFFF"), CardDark = Frozen("#2B2B29");
    private static readonly Brush TextLight = Frozen("#1F1E1C"), TextDark = Frozen("#F1EFE8");
    private static readonly Brush EdgeLight = Frozen("#22000000"), EdgeDark = Frozen("#33FFFFFF");

    public static Brush Track { get; private set; } = TrackLight;
    public static Brush Card { get; private set; } = CardLight;
    public static Brush Text { get; private set; } = TextLight;
    public static Brush Edge { get; private set; } = EdgeLight;
    public static readonly Brush Shadow = Frozen("#2E000000");

    public static readonly Brush CordBase = Frozen("#BA7517");
    public static readonly Brush CordStripe = Frozen("#854F0B");
    public static readonly Brush CordHighlight = Frozen("#FAC775");
    public static readonly Brush BeadLink = Frozen("#6E6D68");
    public static readonly Brush Loop = Frozen("#8E8D87");
    public static readonly Brush BeadRim = Frozen("#994A4946");

    /// <summary>Brushed-steel bead: light from the top left, darker toward the bottom right.</summary>
    public static readonly Brush BeadMetal = FrozenMetal();

    /// <summary>Switches the card, track and text colors to follow the Windows theme.</summary>
    public static void UseDark(bool dark)
    {
        Track = dark ? TrackDark : TrackLight;
        Card = dark ? CardDark : CardLight;
        Text = dark ? TextDark : TextLight;
        Edge = dark ? EdgeDark : EdgeLight;
    }

    public static Brush ForLevel(ColorLevel level, bool grey) => grey ? Grey : level switch
    {
        ColorLevel.Green => Green,
        ColorLevel.Amber => Amber,
        _ => Red,
    };

    private static Brush FrozenMetal()
    {
        var brush = new RadialGradientBrush
        {
            GradientOrigin = new System.Windows.Point(0.32, 0.28),
            Center = new System.Windows.Point(0.42, 0.40),
            RadiusX = 0.72,
            RadiusY = 0.72,
            GradientStops =
            {
                new GradientStop((Color)ColorConverter.ConvertFromString("#FBFAF7"), 0),
                new GradientStop((Color)ColorConverter.ConvertFromString("#CFCEC8"), 0.35),
                new GradientStop((Color)ColorConverter.ConvertFromString("#8E8D87"), 0.8),
                new GradientStop((Color)ColorConverter.ConvertFromString("#5F5E5A"), 1),
            },
        };
        brush.Freeze();
        return brush;
    }

    private static Brush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
