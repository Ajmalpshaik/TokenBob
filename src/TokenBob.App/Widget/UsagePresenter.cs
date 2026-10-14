using System.Windows;
using System.Windows.Media.Animation;
using TokenBob.Core.Alerts;
using TokenBob.Core.Usage;

namespace TokenBob.App.Widget;

/// <summary>Puts a <see cref="UsageDisplayModel"/> on the ornament and hover card.</summary>
internal sealed class UsagePresenter
{
    private readonly WidgetWindow _window;
    private bool _pulsing;

    public UsagePresenter(WidgetWindow window) => _window = window;

    public UsageDisplayModel? Current { get; private set; }

    public void Show(UsageDisplayModel model)
    {
        Current = model;
        var ring = _window.Ornament;
        ring.CenterText = model.CenterText;
        ring.IsGrey = model.IsGrey;
        ring.FiveHourPercent = model.FiveHour?.RingPercent ?? 0;
        ring.WeeklyPercent = model.Weekly?.RingPercent ?? 0;
        ring.FiveHourLevel = model.FiveHour?.Level ?? ColorLevel.Green;
        ring.WeeklyLevel = model.Weekly?.Level ?? ColorLevel.Green;

        var worst = (ColorLevel)Math.Max((int)ring.FiveHourLevel, (int)ring.WeeklyLevel);
        _window.Card.Show(model.HoverLines, Palette.ForLevel(worst, model.IsGrey));

        SetPulse(model.IsLoading);
    }

    private void SetPulse(bool on)
    {
        if (on == _pulsing) return;
        _pulsing = on;
        var ring = _window.Ornament;
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
