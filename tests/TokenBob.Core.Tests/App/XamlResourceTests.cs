using System.Text.RegularExpressions;

namespace TokenBob.Core.Tests.App;

/// <summary>
/// The WPF app can't run here, but its XAML can be read: every resource key must be unique
/// across App.xaml and the control dictionary (a brush and a style sharing a key crashes at
/// start-up), and every StaticResource/DynamicResource a window uses must exist.
/// </summary>
public class XamlResourceTests
{
    private static readonly string AppDir = Path.Combine(RepoRoot(), "src", "TokenBob.App");
    private static readonly Regex KeyPattern = new("x:Key=\"([^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex UsePattern = new("\\{(?:StaticResource|DynamicResource) ([^}\\s]+)\\}", RegexOptions.Compiled);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TokenBob.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("TokenBob.sln not found above the test directory");
    }

    private static IEnumerable<string> AppLevelXaml() =>
        [Path.Combine(AppDir, "App.xaml"), Path.Combine(AppDir, "Ui", "Controls.xaml")];

    [Fact]
    public void Every_app_level_resource_key_is_unique()
    {
        var keys = AppLevelXaml()
            .SelectMany(file => KeyPattern.Matches(File.ReadAllText(file)).Select(m => (Key: m.Groups[1].Value, File: Path.GetFileName(file))))
            .ToList();
        var duplicates = keys.GroupBy(k => k.Key).Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} ({string.Join(", ", g.Select(k => k.File))})").ToList();
        Assert.True(duplicates.Count == 0, "Duplicate resource keys: " + string.Join("; ", duplicates));
    }

    [Fact]
    public void Every_resource_a_window_uses_is_defined()
    {
        var defined = AppLevelXaml()
            .SelectMany(file => KeyPattern.Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value))
            .ToHashSet();
        var missing = new List<string>();
        foreach (var file in Directory.EnumerateFiles(AppDir, "*.xaml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            var local = KeyPattern.Matches(text).Select(m => m.Groups[1].Value).ToHashSet();
            foreach (Match use in UsePattern.Matches(text))
            {
                var key = use.Groups[1].Value;
                if (!defined.Contains(key) && !local.Contains(key)) missing.Add($"{key} in {Path.GetFileName(file)}");
            }
        }
        Assert.True(missing.Count == 0, "Undefined resources: " + string.Join("; ", missing.Distinct()));
    }

    [Fact]
    public void Style_attributes_reference_styles_and_colour_attributes_reference_brushes()
    {
        var text = string.Concat(AppLevelXaml().Select(File.ReadAllText));
        var brushKeys = Regex.Matches(text, "<SolidColorBrush x:Key=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToHashSet();
        var styleKeys = Regex.Matches(text, "<Style x:Key=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToHashSet();
        var wrong = new List<string>();
        foreach (var file in Directory.EnumerateFiles(AppDir, "*.xaml", SearchOption.AllDirectories))
        {
            var xaml = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(xaml, "\\bStyle=\"\\{StaticResource ([^}]+)\\}\""))
                if (!styleKeys.Contains(m.Groups[1].Value)) wrong.Add($"Style={m.Groups[1].Value} in {Path.GetFileName(file)}");
            foreach (Match m in Regex.Matches(xaml, "\\b(?:Background|Foreground|BorderBrush|Fill|Stroke)=\"\\{(?:Static|Dynamic)Resource ([^}]+)\\}\""))
                if (!brushKeys.Contains(m.Groups[1].Value)) wrong.Add($"brush {m.Groups[1].Value} in {Path.GetFileName(file)}");
        }
        Assert.True(wrong.Count == 0, "Resources of the wrong kind: " + string.Join("; ", wrong.Distinct()));
    }

    [Fact]
    public void Styles_and_brushes_never_share_a_key()
    {
        // A key used both as a brush (Ui.* colour) and as a Style would hand a window a colour where it expects a style.
        var text = string.Concat(AppLevelXaml().Select(File.ReadAllText));
        var brushKeys = Regex.Matches(text, "<SolidColorBrush x:Key=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToHashSet();
        var styleKeys = Regex.Matches(text, "<Style x:Key=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToHashSet();
        var shared = brushKeys.Intersect(styleKeys).ToList();
        Assert.True(shared.Count == 0, "Keys used for both a brush and a style: " + string.Join(", ", shared));
    }
}
