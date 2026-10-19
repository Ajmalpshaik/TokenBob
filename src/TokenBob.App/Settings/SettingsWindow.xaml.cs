using System.Windows;
using System.Windows.Controls;
using TokenBob.App.Platform;
using TokenBob.App.Ui;
using TokenBob.Core;
using TokenBob.Core.Config;
using TokenBob.Core.Usage;

namespace TokenBob.App.Settings;

/// <summary>
/// Opened with Ctrl + click or from the tray. Edits the live <see cref="WidgetSettings"/>;
/// every change is applied to the widget (and the preview) straight away.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly WidgetSettings _settings;
    private readonly List<RadioButton> _refreshChoices = new();
    private bool _loading;

    public SettingsWindow(WidgetSettings settings)
    {
        InitializeComponent();
        Theme.Attach(this);
        _settings = settings;
        // Never taller than the screen's work area; the content scrolls instead.
        MaxHeight = Math.Max(480, SystemParameters.WorkArea.Height - 24);

        foreach (var minutes in WidgetSettings.RefreshChoices)
        {
            var choice = new RadioButton
            {
                Content = minutes.ToString(),
                GroupName = "refresh",
                Tag = minutes,
                Style = (Style)FindResource("Ui.Segment"),
                ToolTip = minutes == 1 ? "Every minute" : $"Every {minutes} minutes",
            };
            _refreshChoices.Add(choice);
            RefreshChoices.Children.Add(choice);
        }

        var version = typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3);
        VersionText.Text = $"v{version}";
        AboutLine.Text = $"{AppInfo.Name} {version}";

        Reload();

        foreach (var radio in new[] { ThemeSystem, ThemeLight, ThemeDark, StyleRing, StyleTag, CenterFive, CenterWeek, RopeCord, RopeChain,
                     CordGold, CordGraphite, CordCrimson, CordIvory })
            radio.Checked += (_, _) => Save();
        foreach (var choice in _refreshChoices) choice.Checked += (_, _) => Save();
        foreach (var box in new[] { Wind, BlastToggle, ShiverToggle, HoverToggle, Notifications, StartWithWindows, AutoHide })
        {
            box.Checked += (_, _) => Save();
            box.Unchecked += (_, _) => Save();
        }
        foreach (var slider in new[] { RopeLength, OrnamentSize, OpacitySlider, Swing }) slider.ValueChanged += (_, _) => Save();

        PlayReset.Click += (_, _) => PlayResetRequested?.Invoke();
        ResetPosition.Click += (_, _) => ResetPositionRequested?.Invoke();
        RefreshNow.Click += (_, _) => RefreshRequested?.Invoke();
        SignInOut.Click += (_, _) => SignInOutRequested?.Invoke();
        Quit.Click += (_, _) => QuitRequested?.Invoke();
        RepoLink.Click += (_, _) => Browser.Open(AppInfo.RepoUrl);

        Theme.Changed += OnThemeChanged;
        Closed += (_, _) => Theme.Changed -= OnThemeChanged;
    }

    public event Action? Changed;
    public event Action? RefreshRequested;
    public event Action? SignInOutRequested;
    public event Action? QuitRequested;
    public event Action? PlayResetRequested;
    public event Action? ResetPositionRequested;

    /// <summary>Copies the settings into the controls (also after Shift + drag changes them).</summary>
    public void Reload()
    {
        _loading = true;
        ThemeSystem.IsChecked = _settings.Theme == AppTheme.System;
        ThemeLight.IsChecked = _settings.Theme == AppTheme.Light;
        ThemeDark.IsChecked = _settings.Theme == AppTheme.Dark;
        StyleRing.IsChecked = _settings.Style == OrnamentStyle.Ring;
        StyleTag.IsChecked = _settings.Style == OrnamentStyle.Tag;
        CenterFive.IsChecked = _settings.Center == CenterReadout.FiveHour;
        CenterWeek.IsChecked = _settings.Center == CenterReadout.Weekly;
        RopeCord.IsChecked = _settings.Rope == RopeStyle.TwistedCord;
        RopeChain.IsChecked = _settings.Rope == RopeStyle.BeadChain;
        CordGold.IsChecked = _settings.Cord == CordColor.Gold;
        CordGraphite.IsChecked = _settings.Cord == CordColor.Graphite;
        CordCrimson.IsChecked = _settings.Cord == CordColor.Crimson;
        CordIvory.IsChecked = _settings.Cord == CordColor.Ivory;
        RopeLength.Value = _settings.RopeLength;
        OrnamentSize.Value = _settings.OrnamentScale * 100;
        OpacitySlider.Value = _settings.Opacity * 100;
        Swing.Value = _settings.SwingStrength * 100;
        Wind.IsChecked = _settings.Wind;
        BlastToggle.IsChecked = _settings.ResetBlast;
        ShiverToggle.IsChecked = _settings.Shiver;
        HoverToggle.IsChecked = _settings.HoverCard;
        foreach (var choice in _refreshChoices) choice.IsChecked = (int)choice.Tag == _settings.RefreshMinutes;
        Notifications.IsChecked = _settings.Notifications;
        StartWithWindows.IsChecked = _settings.StartWithWindows;
        AutoHide.IsChecked = _settings.AutoHideFullscreen;
        UpdateDerived();
        _loading = false;
        Preview.Show(_settings, null);
    }

    /// <summary>The latest numbers and status, for the preview and the status lines.</summary>
    public void Update(UsageDisplayModel model, bool signedIn)
    {
        DataStatus.Text = model.HoverLines[^1];
        AccountStatus.Text = signedIn ? $"Signed in · 5-hour {model.FiveHour?.PercentText} · weekly {model.Weekly?.PercentText}" : "Not signed in";
        AccountLine.Text = signedIn ? "Signed in to Claude" : "Not signed in";
        SignInOut.Content = signedIn ? "Sign out" : "Sign in";
        Preview.Show(_settings, model.FiveHour is null ? null : model);
    }

    private void Save()
    {
        UpdateDerived();
        if (_loading) return;

        _settings.Theme = ThemeDark.IsChecked == true ? AppTheme.Dark : ThemeLight.IsChecked == true ? AppTheme.Light : AppTheme.System;
        _settings.Style = StyleTag.IsChecked == true ? OrnamentStyle.Tag : OrnamentStyle.Ring;
        _settings.Center = CenterWeek.IsChecked == true ? CenterReadout.Weekly : CenterReadout.FiveHour;
        _settings.Rope = RopeChain.IsChecked == true ? RopeStyle.BeadChain : RopeStyle.TwistedCord;
        _settings.Cord = CordGraphite.IsChecked == true ? CordColor.Graphite
            : CordCrimson.IsChecked == true ? CordColor.Crimson
            : CordIvory.IsChecked == true ? CordColor.Ivory
            : CordColor.Gold;
        _settings.RopeLength = Math.Round(RopeLength.Value);
        _settings.OrnamentScale = Math.Round(OrnamentSize.Value) / 100;
        _settings.Opacity = Math.Round(OpacitySlider.Value) / 100;
        _settings.SwingStrength = Math.Round(Swing.Value) / 100;
        _settings.Wind = Wind.IsChecked == true;
        _settings.ResetBlast = BlastToggle.IsChecked == true;
        _settings.Shiver = ShiverToggle.IsChecked == true;
        _settings.HoverCard = HoverToggle.IsChecked == true;
        if (_refreshChoices.FirstOrDefault(c => c.IsChecked == true) is { Tag: int minutes }) _settings.RefreshMinutes = minutes;
        _settings.Notifications = Notifications.IsChecked == true;
        _settings.StartWithWindows = StartWithWindows.IsChecked == true;
        _settings.AutoHideFullscreen = AutoHide.IsChecked == true;
        Changed?.Invoke();
        Preview.Show(_settings, null);
    }

    /// <summary>Value labels, and rows that only apply to one choice (ring centre, cord colour).</summary>
    private void UpdateDerived()
    {
        RopeLengthValue.Text = $"{Math.Round(RopeLength.Value)} px";
        OrnamentSizeValue.Text = $"{Math.Round(OrnamentSize.Value)}%";
        OpacityValue.Text = $"{Math.Round(OpacitySlider.Value)}%";
        var ring = StyleRing.IsChecked == true;
        CenterRow.Visibility = CenterDivider.Visibility = ring ? Visibility.Visible : Visibility.Collapsed;
        var cord = RopeCord.IsChecked == true;
        CordRow.Visibility = CordDivider.Visibility = cord ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnThemeChanged() => Preview.Repaint();
}
