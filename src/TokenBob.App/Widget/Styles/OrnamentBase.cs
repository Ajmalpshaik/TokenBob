using System.Globalization;
using System.Windows;
using System.Windows.Media;
using TokenBob.Core.Alerts;

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

    protected const double ShadowOffset = 3;

    protected FormattedText Text(string text, double size, Brush brush, bool semiBold = false) =>
        new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI Variable Display, Segoe UI"), FontStyles.Normal,
                semiBold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
            size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static DependencyProperty Register<T>(string name, T defaultValue) =>
        DependencyProperty.Register(name, typeof(T), typeof(OrnamentBase),
            new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.AffectsRender));
}
