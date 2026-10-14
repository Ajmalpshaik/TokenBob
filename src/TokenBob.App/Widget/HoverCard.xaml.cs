using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TokenBob.App.Widget;

/// <summary>The small card beside the ornament: exact %, reset times, last update.</summary>
public partial class HoverCard : UserControl
{
    public HoverCard() => InitializeComponent();

    public void Show(IReadOnlyList<string> lines, Brush accent)
    {
        Accent.Fill = accent;
        Frame.Background = Palette.Card;
        Lines.Children.Clear();
        for (var i = 0; i < lines.Count; i++)
        {
            var isFooter = lines.Count > 1 && i == lines.Count - 1;
            Lines.Children.Add(new TextBlock
            {
                Text = lines[i],
                FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI"),
                FontSize = isFooter ? 11.5 : 13,
                Foreground = isFooter ? Palette.Grey : Palette.Text,
                Margin = new Thickness(0, i == 0 ? 0 : isFooter ? 5 : 2, 0, 0),
            });
        }
    }
}
