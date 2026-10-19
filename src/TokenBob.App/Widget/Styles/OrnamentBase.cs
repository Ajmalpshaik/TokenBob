using System.Globalization;
using System.Windows;
using System.Windows.Media;
using TokenBob.Core.Alerts;
using TokenBob.Core.Physics;

namespace TokenBob.App.Widget.Styles;

/// <summary>
/// What every ornament style shows. The hang point (where the rope ends) is the top
/// center of the element.
/// </summary>
internal abstract class OrnamentBase : FrameworkElement
{
    public static readonly DependencyProperty FiveHourPercentProperty = Register(nameof(FiveHourPercent), 0.0);
    public static readonly DependencyProperty WeeklyPercentProperty = Register(nameof(WeeklyPercent), 0.0);
    public static readonly DependencyProperty FiveHourTextProperty = Register(nameof(FiveHourText), "");
    public static readonly DependencyProperty WeeklyTextProperty = Register(nameof(WeeklyText), "");
    public static readonly DependencyProperty FiveHourResetProperty = Register(nameof(FiveHourReset), "");
    public static readonly DependencyProperty WeeklyResetProperty = Register(nameof(WeeklyReset), "");
    public static readonly DependencyProperty CenterTextProperty = Register(nameof(CenterText), "");
    public static readonly DependencyProperty HasDataProperty = Register(nameof(HasData), false);
    public static readonly DependencyProperty FiveHourLevelProperty = Register(nameof(FiveHourLevel), ColorLevel.Green);
    public static readonly DependencyProperty WeeklyLevelProperty = Register(nameof(WeeklyLevel), ColorLevel.Green);
    public static readonly DependencyProperty IsGreyProperty = Register(nameof(IsGrey), false);
    public static readonly DependencyProperty ScaleProperty = DependencyProperty.Register(
        nameof(Scale), typeof(double), typeof(OrnamentBase),
        new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double FiveHourPercent { get => (double)GetValue(FiveHourPercentProperty); set => SetValue(FiveHourPercentProperty, value); }
    public double WeeklyPercent { get => (double)GetValue(WeeklyPercentProperty); set => SetValue(WeeklyPercentProperty, value); }
    public string FiveHourText { get => (string)GetValue(FiveHourTextProperty); set => SetValue(FiveHourTextProperty, value); }
    public string WeeklyText { get => (string)GetValue(WeeklyTextProperty); set => SetValue(WeeklyTextProperty, value); }
    public string FiveHourReset { get => (string)GetValue(FiveHourResetProperty); set => SetValue(FiveHourResetProperty, value); }
    public string WeeklyReset { get => (string)GetValue(WeeklyResetProperty); set => SetValue(WeeklyResetProperty, value); }
    public string CenterText { get => (string)GetValue(CenterTextProperty); set => SetValue(CenterTextProperty, value); }
    public bool HasData { get => (bool)GetValue(HasDataProperty); set => SetValue(HasDataProperty, value); }
    public ColorLevel FiveHourLevel { get => (ColorLevel)GetValue(FiveHourLevelProperty); set => SetValue(FiveHourLevelProperty, value); }
    public ColorLevel WeeklyLevel { get => (ColorLevel)GetValue(WeeklyLevelProperty); set => SetValue(WeeklyLevelProperty, value); }
    public bool IsGrey { get => (bool)GetValue(IsGreyProperty); set => SetValue(IsGreyProperty, value); }
    public double Scale { get => (double)GetValue(ScaleProperty); set => SetValue(ScaleProperty, value); }

    /// <summary>Distance from the hang point to the middle of the ornament (for wind and grabbing).</summary>
    public abstract double CenterOffset { get; }

    /// <summary>
    /// The ornament cut into the pieces that fly apart on a limit reset, each placed by its
    /// offset from the ornament's centre, so the blast starts from exactly what was drawn.
    /// </summary>
    public abstract IReadOnlyList<BurstPiece> Shatter();

    protected const double ShadowOffset = 3;

    protected BurstPaint FiveHourPaint => Palette.PaintFor(FiveHourLevel, IsGrey);

    protected BurstPaint WeeklyPaint => Palette.PaintFor(WeeklyLevel, IsGrey);

    /// <summary>A ring split into <paramref name="count"/> arcs; the used share keeps its colour, the rest is track.</summary>
    protected static void RingPieces(List<BurstPiece> pieces, double radius, double thickness, double percent, BurstPaint paint, int count)
    {
        var used = Math.Clamp(percent, 0, 100) / 100 * 360;
        var span = 360.0 / count;
        for (var i = 0; i < count; i++)
        {
            var mid = (i + 0.5) * span;
            var (sin, cos) = Math.SinCos(mid * Math.PI / 180);
            pieces.Add(new BurstArc(new Vec2(radius * sin, -radius * cos), radius, span, thickness, mid < used ? paint : BurstPaint.Track));
        }
    }

    /// <summary>A bar split into <paramref name="count"/> pieces across its filled width.</summary>
    protected static void BarPieces(List<BurstPiece> pieces, double left, double centerY, double filledWidth, double height, BurstPaint paint, int count)
    {
        var width = filledWidth / count;
        for (var i = 0; i < count; i++)
            pieces.Add(new BurstShard(new Vec2(left + width * (i + 0.5), centerY), Math.Max(2, width - 1), height, height / 2, paint));
    }

    protected FormattedText Text(string text, double size, Brush brush, bool semiBold = false) =>
        new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal,
                semiBold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
            size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static DependencyProperty Register<T>(string name, T defaultValue) =>
        DependencyProperty.Register(name, typeof(T), typeof(OrnamentBase),
            new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.AffectsRender));
}
