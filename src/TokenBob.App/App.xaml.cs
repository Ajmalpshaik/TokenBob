using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using TokenBob.App.Account;
using TokenBob.App.Input;
using TokenBob.App.Platform;
using TokenBob.App.Settings;
using TokenBob.App.Tray;
using TokenBob.App.Widget;
using TokenBob.Core;
using TokenBob.Core.Alerts;
using TokenBob.Core.Config;
using TokenBob.Core.Logging;
using TokenBob.Core.Physics;
using TokenBob.Core.Screens;
using TokenBob.Core.Usage;

namespace TokenBob.App;

public partial class App : Application
{
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(500);

    private Mutex? _singleInstance;
    private ILog _log = NullLog.Instance;
    private WidgetSettings _settings = new();
    private JsonStore<WidgetSettings>? _settingsStore;
    private DispatcherTimer? _saveTimer;
    private WidgetWindow? _widget;
    private GestureController? _gestures;
    private UsagePresenter? _presenter;
    private TrayIconHost? _tray;
    private WebView2Fetcher? _fetcher;
    private UsagePoller? _poller;
    private DispatcherTimer? _minuteTimer;
    private SignInWindow? _signIn;
    private SettingsWindow? _settingsWindow;
    private bool _signInOfferedThisLaunch;
    private JsonStore<AlertMemory>? _alertStore;
    private AlertMemory _alertMemory = new();
    private UsageSnapshot? _lastEvaluated;
    private bool _demo;
    private FullscreenWatcher? _fullscreen;
    private bool _hiddenByUser;
    private bool _hiddenForFullscreen;

    private bool SignedIn => _poller?.State is { LastGood: not null, Status: not UsageError.NotSignedIn };

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args is ["--render-samples", var samplePath])
        {
            SampleSheet.Render(samplePath);
            Shutdown();
            return;
        }

        _demo = e.Args is ["--demo-alerts"];

        _singleInstance = new Mutex(initiallyOwned: true, @"Local\TokenBob.SingleInstance", out var firstInstance);
        if (!firstInstance)
        {
            Shutdown();
            return;
        }

        _log = new FileLog(AppPaths.LogsDir, TimeProvider.System);
        DispatcherUnhandledException += (_, args) =>
        {
            _log.Error("Unhandled UI exception", args.Exception);
            args.Handled = true;
        };
        _log.Info($"TokenBob {typeof(App).Assembly.GetName().Version} starting");

        _alertStore = new JsonStore<AlertMemory>(AppPaths.AlertStateFile, _log);
        if (!_demo) _alertMemory = _alertStore.Load();

        _settingsStore = new JsonStore<WidgetSettings>(AppPaths.SettingsFile, _log);
        _settings = _settingsStore.Load().Normalized();
        _saveTimer = new DispatcherTimer(SaveDelay, DispatcherPriority.Background, (_, _) => SaveSettingsNow(), Dispatcher) { IsEnabled = false };
        SyncStartup();

        Palette.UseDark(WindowsTheme.IsDark);
        WindowsTheme.Changed += () =>
        {
            Palette.UseDark(WindowsTheme.IsDark);
            _widget?.Repaint();
            Render();
        };

        var placement = Placement.Resolve(Monitors.GetAll(), _settings.Monitor, _settings.AnchorXDevice, _settings.RopeLength);
        _widget = new WidgetWindow(_settings, placement);
        _presenter = new UsagePresenter(_widget);

        _tray = new TrayIconHost();
        _tray.SettingsClicked += OpenSettings;
        _tray.ToggleClicked += ToggleWidget;
        _tray.RefreshClicked += () => _poller?.RefreshNow();
        _tray.SignInOutClicked += async () => await SignInOrOutAsync();
        _tray.QuitClicked += Shutdown;
        _widget.OrnamentHost.ContextMenu = _tray.BuildMenu();

        _gestures = new GestureController(_widget, _settings, Monitors.GetAll);
        _gestures.SettingsRequested += OpenSettings;
        _gestures.PullRefreshRequested += OnPullRefresh;
        _gestures.PlacementChanged += SavePlacement;
        _gestures.UsagePageRequested += () =>
        {
            _log.Info("Opening usage page");
            Browser.Open(AppInfo.UsagePageUrl);
        };
        _widget.Show();

        _fullscreen = new FullscreenWatcher(() => _widget.Placement.Monitor, Dispatcher) { Enabled = _settings.AutoHideFullscreen };
        _fullscreen.FullscreenChanged += fullscreen =>
        {
            _hiddenForFullscreen = fullscreen;
            _log.Info(fullscreen ? "Fullscreen app: hiding widget" : "Fullscreen app closed: showing widget");
            UpdateWidgetVisibility();
        };

        _fetcher = new WebView2Fetcher();
        _fetcher.Released += MemoryTrim.Trim;
        IUsageProvider provider = _demo ? new DemoUsageProvider() : new ClaudeWebUsageProvider(_fetcher, TimeProvider.System, _log);
        _poller = new UsagePoller(provider, TimeProvider.System, TimeSpan.FromMinutes(_settings.RefreshMinutes));
        _poller.Updated += OnUsage;
        _poller.Start();
        if (_demo) StartDemo((DemoUsageProvider)provider);

        _minuteTimer = new DispatcherTimer(TimeSpan.FromSeconds(60), DispatcherPriority.Background, (_, _) => Render(), Dispatcher);
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _log.Info("TokenBob exiting");
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        if (_saveTimer?.IsEnabled == true) SaveSettingsNow();
        _minuteTimer?.Stop();
        _poller?.Dispose();
        _fetcher?.Dispose();
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void OnUsage(UsageState state)
    {
        Render();
        if (state is { Status: null, IsLoading: false, LastGood: { } fresh } && !ReferenceEquals(fresh, _lastEvaluated))
        {
            RunAlerts(_lastEvaluated, fresh);
            _lastEvaluated = fresh;
        }
        if (state is { Status: UsageError.NotSignedIn, IsLoading: false } && !_signInOfferedThisLaunch)
        {
            _signInOfferedThisLaunch = true;
            ShowSignIn();
        }
    }

    private void RunAlerts(UsageSnapshot? previous, UsageSnapshot current)
    {
        var events = AlertRules.Evaluate(previous, current, _alertMemory);
        if (events.Count == 0) return;
        if (!_demo) _alertStore?.Save(_alertMemory);

        foreach (var alert in events)
        {
            switch (alert)
            {
                case ThresholdCrossed crossing when _settings.Notifications:
                    var (title, body) = UsageDisplay.NotificationText(crossing, current, DateTimeOffset.Now, TimeZoneInfo.Local, CultureInfo.CurrentCulture);
                    _tray?.Notify(title, body);
                    _log.Info($"Notified: {crossing.Limit} {crossing.Threshold}%");
                    break;
                case Shiver:
                    _widget?.Shiver();
                    break;
                case ResetBounce:
                    _widget?.Sim.ApplyImpulse(new Vec2(0, -900));
                    _widget?.Wake();
                    break;
            }
        }
    }

    /// <summary>Developer demo: fetch the next fake step every 4 seconds until the reset.</summary>
    private void StartDemo(DemoUsageProvider provider)
    {
        _log.Info("Alert demo running");
        var timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromSeconds(4) };
        timer.Tick += (_, _) =>
        {
            if (provider.Finished) timer.Stop();
            else _poller?.RefreshNow(ignoreCooldown: true);
        };
        timer.Start();
    }

    private void Render()
    {
        if (_poller is null || _presenter is null) return;
        var model = UsageDisplay.From(_poller.State, DateTimeOffset.Now, TimeZoneInfo.Local, CultureInfo.CurrentCulture);
        _presenter.Show(model);
        _tray?.SetTooltip(model.TrayTooltip);
        _tray?.SetSignedIn(SignedIn);
        _settingsWindow?.SetStatus(model.HoverLines[^1], SignedIn);
    }

    private void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }
        SyncStartup();
        _settingsWindow = new SettingsWindow(_settings);
        _settingsWindow.Changed += OnSettingsChanged;
        _settingsWindow.RefreshRequested += () => _poller?.RefreshNow();
        _settingsWindow.SignInOutRequested += async () => await SignInOrOutAsync();
        _settingsWindow.QuitRequested += Shutdown;
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        Render();
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OnSettingsChanged()
    {
        _widget?.ApplySettings();
        if (_poller is not null) _poller.Interval = TimeSpan.FromMinutes(_settings.RefreshMinutes);
        if (_settings.StartWithWindows != StartupRegistration.IsEnabled()) TrySetStartup(_settings.StartWithWindows);
        if (_fullscreen is not null) _fullscreen.Enabled = _settings.AutoHideFullscreen;
        _saveTimer?.Stop();
        _saveTimer?.Start();
    }

    private void SaveSettingsNow()
    {
        _saveTimer?.Stop();
        _settingsStore?.Save(_settings);
    }

    /// <summary>
    /// Keeps the startup entry pointing at this exe, adds Windows' approval if it's missing,
    /// and shows what Windows will really do (the user may have switched it off there).
    /// </summary>
    private void SyncStartup()
    {
        try
        {
            StartupRegistration.Refresh();
            _settings.StartWithWindows = StartupRegistration.IsEnabled();
        }
        catch (Exception ex)
        {
            _log.Error("Couldn't check start with Windows", ex);
        }
    }

    private void TrySetStartup(bool enabled)
    {
        try
        {
            StartupRegistration.Set(enabled);
        }
        catch (Exception ex)
        {
            _log.Error("Couldn't change start with Windows", ex);
        }
    }

    private void ToggleWidget()
    {
        _hiddenByUser = !_hiddenByUser;
        UpdateWidgetVisibility();
    }

    private void UpdateWidgetVisibility()
    {
        var visible = !_hiddenByUser && !_hiddenForFullscreen;
        _widget?.FadeTo(visible);
        _tray?.SetWidgetVisible(!_hiddenByUser);
    }

    private async Task SignInOrOutAsync()
    {
        if (!SignedIn)
        {
            ShowSignIn();
            return;
        }
        if (_fetcher is null) return;
        await _fetcher.SignOutAsync();
        _log.Info("Signed out of claude.ai");
        _poller?.RefreshNow(ignoreCooldown: true);
    }

    private void ShowSignIn()
    {
        if (_signIn is not null)
        {
            _signIn.Activate();
            return;
        }
        _signIn = new SignInWindow(_log);
        _signIn.SignedIn += () => _poller?.RefreshNow(ignoreCooldown: true);
        _signIn.Closed += (_, _) => _signIn = null;
        _signIn.Show();
    }

    private void OnPullRefresh()
    {
        if (_poller?.RefreshNow() != true || _widget is null) return;
        _log.Info("Pull to refresh");
        _widget.Sim.ApplyImpulse(new Vec2(0, -600));
        _widget.Wake();
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) Dispatcher.BeginInvoke(() => _poller?.RefreshNow(ignoreCooldown: true));
    }

    private void SavePlacement(PlacementResult placement)
    {
        _settings.Monitor = Placement.IdOf(placement.Monitor);
        _settings.AnchorXDevice = placement.AnchorXDevice;
        _settings.RopeLength = placement.RopeLength;
        SaveSettingsNow();
        _settingsWindow?.Reload();
        _log.Info("Placement saved");
    }
}
