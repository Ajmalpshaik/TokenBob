using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using static TokenBob.App.Platform.NativeMethods;

namespace TokenBob.App.Ui;

/// <summary>
/// The colours of every normal window (Settings, Install, Sign in), light or dark. Windows
/// use them through <c>{DynamicResource Ui.*}</c>, so switching re-paints them live.
/// </summary>
internal static class Theme
{
    private static readonly (string Key, string Light, string Dark)[] Brushes =
    [
        ("Ui.Window", "#F6F4EF", "#1B1B1A"),
        ("Ui.Card", "#FFFFFF", "#262625"),
        ("Ui.CardEdge", "#E4E2DA", "#363634"),
        ("Ui.Text", "#1F1E1C", "#F1EFE8"),
        ("Ui.Muted", "#6F6D66", "#A6A49C"),
        ("Ui.Accent", "#BA7517", "#E0A23A"),
        ("Ui.AccentText", "#FFFFFF", "#1F1E1C"),
        ("Ui.Control", "#EEEBE4", "#30302E"),
        ("Ui.ControlHover", "#E4E0D6", "#3B3B38"),
        ("Ui.SegmentOn", "#FFFFFF", "#4A4A45"),
        ("Ui.Track", "#D8D4CA", "#4A4A47"),
        ("Ui.Thumb", "#FFFFFF", "#F1EFE8"),
        ("Ui.Stage", "#ECE9E1", "#202020"),
        ("Ui.Danger", "#C9452F", "#E0533D"),
    ];

    public static bool IsDark { get; private set; }

    /// <summary>Raised after the colours were switched (windows re-paint their title bars).</summary>
    public static event Action? Changed;

    public static void Apply(bool dark)
    {
        IsDark = dark;
        var resources = Application.Current.Resources;
        foreach (var (key, light, darkHex) in Brushes)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? darkHex : light));
            brush.Freeze();
            resources[key] = brush;
        }
        Changed?.Invoke();
    }

    /// <summary>Gives a window a title bar that matches the theme, now and whenever it changes.</summary>
    public static void Attach(Window window)
    {
        void Paint() => PaintTitleBar(window);
        window.SourceInitialized += (_, _) => Paint();
        Changed += Paint;
        window.Closed += (_, _) => Changed -= Paint;
        if (new WindowInteropHelper(window).Handle != IntPtr.Zero) Paint();
    }

    private static void PaintTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var dark = IsDark ? 1 : 0;
        try
        {
            DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Older Windows: the title bar stays as it is.
        }
    }
}
