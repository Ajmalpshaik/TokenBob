using System.Windows.Media;
using TokenBob.Core.Alerts;

namespace TokenBob.App.Widget;

/// <summary>All widget colors in one place. Light theme for now; dark variants arrive with theme support.</summary>
internal static class Palette
{
    public static readonly Brush Green = Frozen("#3FA45B");
    public static readonly Brush Amber = Frozen("#E8A23A");
    public static readonly Brush Red = Frozen("#E0533D");
    public static readonly Brush Grey = Frozen("#9A988F");

    public static Brush Track { get; private set; } = Frozen("#E4E2DA");
    public static Brush Card { get; private set; } = Frozen("#FFFFFF");
    public static Brush Text { get; private set; } = Frozen("#1F1E1C");
    public static readonly Brush Shadow = Frozen("#2E000000");

    public static readonly Brush CordBase = Frozen("#BA7517");
    public static readonly Brush CordStripe = Frozen("#854F0B");
    public static readonly Brush CordHighlight = Frozen("#FAC775");

    public static Brush ForLevel(ColorLevel level, bool grey) => grey ? Grey : level switch
    {
        ColorLevel.Green => Green,
        ColorLevel.Amber => Amber,
        _ => Red,
    };

    private static Brush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
