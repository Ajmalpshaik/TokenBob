using System.Windows;
using System.Windows.Controls;
using TokenBob.App.Platform;
using TokenBob.Core;
using TokenBob.Core.Config;

namespace TokenBob.App.Settings;

/// <summary>
/// Opened with Ctrl + click or from the tray. Edits the live <see cref="WidgetSettings"/>;
/// every change is applied to the widget straight away.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly WidgetSettings _settings;
    private bool _loading;

    public SettingsWindow(WidgetSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        foreach (var minutes in WidgetSettings.RefreshChoices)
            Refresh.Items.Add(new ComboBoxItem { Content = minutes == 1 ? "1 minute" : $"{minutes} minutes", Tag = minutes });
        VersionText.Text = $"{AppInfo.Name} {typeof(SettingsWindow).Assembly.GetName().Version?.ToString(3)}";

        Reload();

        foreach (var radio in new[] { StyleRing, StyleTag, RopeCord, RopeChain }) radio.Checked += (_, _) => Save();
        foreach (var box in new[] { Wind, Notifications, StartWithWindows, AutoHide })
        {
            box.Checked += (_, _) => Save();
            box.Unchecked += (_, _) => Save();
        }
        foreach (var slider in new[] { RopeLength, OrnamentSize, Swing }) slider.ValueChanged += (_, _) => Save();
        Refresh.SelectionChanged += (_, _) => Save();

        RefreshNow.Click += (_, _) => RefreshRequested?.Invoke();
        SignInOut.Click += (_, _) => SignInOutRequested?.Invoke();
        Quit.Click += (_, _) => QuitRequested?.Invoke();
        RepoLink.Click += (_, _) => Browser.Open(AppInfo.RepoUrl);
    }

    public event Action? Changed;
    public event Action? RefreshRequested;
    public event Action? SignInOutRequested;
    public event Action? QuitRequested;

    /// <summary>Copies the settings into the controls (also after Shift + drag changes them).</summary>
    public void Reload()
    {
        _loading = true;
        StyleRing.IsChecked = _settings.Style == OrnamentStyle.Ring;
        StyleTag.IsChecked = _settings.Style == OrnamentStyle.Tag;
        RopeCord.IsChecked = _settings.Rope == RopeStyle.TwistedCord;
        RopeChain.IsChecked = _settings.Rope == RopeStyle.BeadChain;
        RopeLength.Value = _settings.RopeLength;
        OrnamentSize.Value = _settings.OrnamentScale * 100;
        Swing.Value = _settings.SwingStrength * 100;
        Wind.IsChecked = _settings.Wind;
        Refresh.SelectedItem = Refresh.Items.Cast<ComboBoxItem>().First(i => (int)i.Tag == _settings.RefreshMinutes);
        Notifications.IsChecked = _settings.Notifications;
        StartWithWindows.IsChecked = _settings.StartWithWindows;
        AutoHide.IsChecked = _settings.AutoHideFullscreen;
        UpdateValueLabels();
        _loading = false;
    }

    public void SetStatus(string dataStatus, bool signedIn)
    {
        DataStatus.Text = dataStatus;
        AccountStatus.Text = signedIn ? "Signed in to Claude" : "Not signed in";
        SignInOut.Content = signedIn ? "Sign out" : "Sign in";
    }

    private void Save()
    {
        UpdateValueLabels();
        if (_loading) return;

        _settings.Style = StyleTag.IsChecked == true ? OrnamentStyle.Tag : OrnamentStyle.Ring;
        _settings.Rope = RopeChain.IsChecked == true ? RopeStyle.BeadChain : RopeStyle.TwistedCord;
        _settings.RopeLength = Math.Round(RopeLength.Value);
        _settings.OrnamentScale = Math.Round(OrnamentSize.Value) / 100;
        _settings.SwingStrength = Math.Round(Swing.Value) / 100;
        _settings.Wind = Wind.IsChecked == true;
        if (Refresh.SelectedItem is ComboBoxItem { Tag: int minutes }) _settings.RefreshMinutes = minutes;
        _settings.Notifications = Notifications.IsChecked == true;
        _settings.StartWithWindows = StartWithWindows.IsChecked == true;
        _settings.AutoHideFullscreen = AutoHide.IsChecked == true;
        Changed?.Invoke();
    }

    private void UpdateValueLabels()
    {
        RopeLengthValue.Text = $"{Math.Round(RopeLength.Value)} px";
        OrnamentSizeValue.Text = $"{Math.Round(OrnamentSize.Value)}%";
    }
}
