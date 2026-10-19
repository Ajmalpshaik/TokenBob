using System.Windows;
using System.Windows.Media.Animation;
using TokenBob.App.Widget.Styles;
using TokenBob.Core.Alerts;
using TokenBob.Core.Config;
using TokenBob.Core.Usage;

namespace TokenBob.App.Widget;

/// <summary>Puts a <see cref="UsageDisplayModel"/> on the ornament and hover card.</summary>
internal sealed class UsagePresenter
{
    private readonly WidgetWindow _window;
    private readonly WidgetSettings _settings;
    private bool _pulsing;

    public UsagePresenter(WidgetWindow window, WidgetSettings settings)
    {
        _window = window;
        _settings = settings;
        window.OrnamentReplaced += () =>
        {
            if (Current is { } model) Show(model);
        };
    }

    public UsageDisplayModel? Current { get; private set; }

    public void Show(UsageDisplayModel model)
    {
        Current = model;
        var ornament = _window.Ornament;
        Fill(ornament, model, _settings.Center);

        var worst = (ColorLevel)Math.Max((int)ornament.FiveHourLevel, (int)ornament.WeeklyLevel);
        _window.Card.Show(model.HoverLines, Palette.ForLevel(worst, model.IsGrey));

        SetPulse(model.IsLoading);
    }

    /// <summary>Copies a display model onto any ornament style.</summary>
    public static void Fill(OrnamentBase ornament, UsageDisplayModel model, CenterReadout center = CenterReadout.FiveHour)
    {
        ornament.CenterText = center == CenterReadout.Weekly && model.Weekly is { } weekly ? weekly.PercentText : model.CenterText;
        ornament.HasData = model.FiveHour is not null;
        ornament.FiveHourText = model.FiveHour?.PercentText ?? "";
        ornament.WeeklyText = model.Weekly?.PercentText ?? "";
        ornament.FiveHourReset = model.FiveHour?.ResetShort ?? "";
        ornament.WeeklyReset = model.Weekly?.ResetShort ?? "";
        ornament.IsGrey = model.IsGrey;
        ornament.FiveHourPercent = model.FiveHour?.RingPercent ?? 0;
        ornament.WeeklyPercent = model.Weekly?.RingPercent ?? 0;
        ornament.FiveHourLevel = model.FiveHour?.Level ?? ColorLevel.Green;
        ornament.WeeklyLevel = model.Weekly?.Level ?? ColorLevel.Green;
    }

    private void SetPulse(bool on)
    {
        if (on == _pulsing) return;
        _pulsing = on;
        var ring = _window.OrnamentHost;
        if (on)
        {
            ring.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1, 0.45, TimeSpan.FromSeconds(0.9))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase(),
            });
        }
        else
        {
            ring.BeginAnimation(UIElement.OpacityProperty, null);
        }
    }
}
