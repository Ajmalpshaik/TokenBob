using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TokenBob.App.Widget.Styles;
using TokenBob.Core.Config;
using TokenBob.Core.Physics;
using TokenBob.Core.Usage;

namespace TokenBob.App.Widget;

/// <summary>
/// Developer tool: <c>TokenBob.exe --render-samples out.png</c> draws every ornament and
/// rope style, light and dark, into one picture (for design checks and README images).
/// </summary>
internal static class SampleSheet
{
    private const double Cell = 260;
    private const double CellHeight = 330;

    public static void Render(string path)
    {
        var now = DateTimeOffset.Now;
        var snapshot = new UsageSnapshot(new LimitUsage(42, now.AddHours(2).AddMinutes(10)),
            new LimitUsage(71, now.AddDays(4)), now.AddMinutes(-2));
        var model = UsageDisplay.From(new UsageState(snapshot, null, false), now, TimeZoneInfo.Local, CultureInfo.CurrentCulture);

        var combos = (from dark in new[] { false, true }
                      from style in new[] { OrnamentStyle.Ring, OrnamentStyle.Tag }
                      from rope in new[] { RopeStyle.TwistedCord, RopeStyle.BeadChain }
                      select (dark, style, rope)).ToList();

        var sheet = new DrawingVisual();
        using (var dc = sheet.RenderOpen())
        {
            for (var i = 0; i < combos.Count; i++)
            {
                var (dark, style, rope) = combos[i];
                Palette.UseDark(dark);
                var cell = DrawCell(style, rope, model, swing: i % 2 == 1, dark);
                cell.Measure(new Size(Cell, CellHeight));
                cell.Arrange(new Rect(0, 0, Cell, CellHeight));
                cell.UpdateLayout();
                var image = new RenderTargetBitmap((int)Cell * 2, (int)CellHeight * 2, 192, 192, PixelFormats.Pbgra32);
                image.Render(cell);
                dc.DrawImage(image, new Rect(i % 4 * Cell, i / 4 * CellHeight, Cell, CellHeight));
            }
        }
        Palette.UseDark(false);

        var bitmap = new RenderTargetBitmap((int)Cell * 8, (int)CellHeight * 4, 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(sheet);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var file = File.Create(path);
        encoder.Save(file);
    }

    private static FrameworkElement DrawCell(OrnamentStyle style, RopeStyle rope, UsageDisplayModel model, bool swing, bool dark)
    {
        var cell = new Canvas
        {
            Width = Cell,
            Height = CellHeight,
            Background = new SolidColorBrush(dark ? Color.FromRgb(0x20, 0x20, 0x1E) : Color.FromRgb(0xEC, 0xEA, 0xE3)),
        };

        var sim = new RopeSim(new Vec2(Cell / 2, 0), 150, 0.5);
        for (var t = 0; t < 60; t++) sim.Advance(1 / 60.0);
        if (swing)
        {
            sim.ApplyImpulse(new Vec2(900, 0));
            for (var t = 0; t < 14; t++) sim.Advance(1 / 60.0);
        }

        var ropeVisual = new RopeVisual { Width = Cell, Height = CellHeight, RopeStyle = rope };
        ropeVisual.Update(sim.Points);
        cell.Children.Add(ropeVisual);

        OrnamentBase ornament = style == OrnamentStyle.Tag ? new HangingTag() : new RingOrnament();
        UsagePresenter.Fill(ornament, model);
        ornament.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var hang = sim.OrnamentPosition;
        Canvas.SetLeft(ornament, hang.X - ornament.HangX);
        Canvas.SetTop(ornament, hang.Y);
        ornament.RenderTransform = new RotateTransform(sim.OrnamentAngle * 180 / Math.PI, ornament.HangX, 0);
        cell.Children.Add(ornament);

        cell.Children.Add(new TextBlock
        {
            Text = $"{style} · {rope} · {(dark ? "dark" : "light")}",
            Foreground = Palette.Grey,
            FontSize = 11,
            Margin = new Thickness(8, CellHeight - 22, 0, 0),
        });
        return cell;
    }
}
