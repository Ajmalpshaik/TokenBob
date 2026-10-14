using System.Windows;
using System.Windows.Media;

namespace TokenBob.App.Widget.Styles;

/// <summary>
/// Style B: a rounded tag with two labelled bars (5-hour, Weekly), each with its % and
/// time until reset.
/// </summary>
internal sealed class HangingTag : OrnamentBase
{
    private const double Width0 = 124;
    private const double Height0 = 92;
    private const double Top = 4;          // short string from the hang point down to the eyelet
    private const double Pad = 12;

    public override double CenterOffset => (Top + Height0 / 2) * Scale;

    protected override Size MeasureOverride(Size availableSize) =>
        new(Width0 * Scale + ShadowOffset, (Top + Height0) * Scale + ShadowOffset);

    protected override void OnRender(DrawingContext dc)
    {
        var s = Scale;
        var w = Width0 * s;
        var card = new Rect(0, Top * s, w, Height0 * s);
        var eyelet = new Point(w / 2, (Top + 7) * s);

        dc.DrawLine(new Pen(Palette.Loop, 1.4), new Point(w / 2, 0), eyelet);
        dc.DrawRoundedRectangle(Palette.Shadow, null, new Rect(card.X + ShadowOffset, card.Y + ShadowOffset, card.Width, card.Height), 12 * s, 12 * s);
        dc.DrawRoundedRectangle(Palette.Card, new Pen(Palette.Track, 1), card, 12 * s, 12 * s);
        dc.DrawEllipse(Palette.Track, new Pen(Palette.Grey, 1), eyelet, 2.6 * s, 2.6 * s);

        if (!HasData)
        {
            var message = Text(CenterText, 22 * s, Palette.Grey, semiBold: true);
            dc.DrawText(message, new Point(w / 2 - message.Width / 2, card.Y + card.Height / 2 - message.Height / 2 + 4 * s));
            return;
        }

        DrawRow(dc, s, card.Y + 15 * s, "5-hour", FiveHourText, FiveHourReset, FiveHourPercent, Palette.ForLevel(FiveHourLevel, IsGrey));
        DrawRow(dc, s, card.Y + 52 * s, "Weekly", WeeklyText, WeeklyReset, WeeklyPercent, Palette.ForLevel(WeeklyLevel, IsGrey));
    }

    private void DrawRow(DrawingContext dc, double s, double y, string label, string percent, string reset, double value, Brush color)
    {
        var left = Pad * s;
        var right = (Width0 - Pad) * s;
        var textBrush = IsGrey ? Palette.Grey : Palette.Text;

        var labelText = Text(label, 11 * s, Palette.Grey);
        dc.DrawText(labelText, new Point(left, y));
        var percentText = Text(percent, 11 * s, textBrush, semiBold: true);
        dc.DrawText(percentText, new Point(right - percentText.Width, y));

        var barY = y + 15 * s;
        var barWidth = right - left;
        var bar = new Rect(left, barY, barWidth, 5 * s);
        dc.DrawRoundedRectangle(Palette.Track, null, bar, 2.5 * s, 2.5 * s);
        var share = Math.Clamp(value, 0, 100) / 100;
        if (share > 0)
            dc.DrawRoundedRectangle(color, null, new Rect(left, barY, Math.Max(5 * s, barWidth * share), 5 * s), 2.5 * s, 2.5 * s);

        var resetText = Text(reset, 9.5 * s, Palette.Grey);
        dc.DrawText(resetText, new Point(left, barY + 7 * s));
    }
}
