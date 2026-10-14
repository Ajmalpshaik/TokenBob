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

    private bool SignedIn => _poller?.State is { LastGood: not null, Status: not UsageError.NotSignedIn };

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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

        _settingsStore = new JsonStore<WidgetSettings>(AppPaths.SettingsFile, _log);
        _settings = _settingsStore.Load().Normalized();
        _saveTimer = new DispatcherTimer(SaveDelay, DispatcherPriority.Background, (_, _) => SaveSettingsNow(), Dispatcher) { IsEnabled = false };
        if (_settings.StartWithWindows) TrySetStartup(true);   // keeps the path right if the exe moved

        var placement = Placement.Resolve(Monitors.GetAll(), _settings.Monitor, _settings.AnchorXDevice, _settings.RopeLength);
        _widget = new WidgetWindow(_settings, placement);
        _presenter = new UsagePresenter(_widget);

        _tray = new TrayIconHost();
        _tray.SettingsClicked += OpenSettings;
        _tray.ToggleClicked += ToggleWidget;
        _tray.RefreshClicked += () => _poller?.RefreshNow();
        _tray.SignInOutClicked += async () => await SignInOrOutAsync();
        _tray.QuitClicked += Shutdown;
        _widget.Ornament.ContextMenu = _tray.BuildMenu();

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

        _fetcher = new WebView2Fetcher();
        _poller = new UsagePoller(new ClaudeWebUsageProvider(_fetcher, TimeProvider.System, _log),
            TimeProvider.System, TimeSpan.FromMinutes(_settings.RefreshMinutes));
        _poller.Updated += OnUsage;
        _poller.Start();

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
        if (state is { Status: UsageError.NotSignedIn, IsLoading: false } && !_signInOfferedThisLaunch)
        {
            _signInOfferedThisLaunch = true;
            ShowSignIn();
        }
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
        _saveTimer?.Stop();
        _saveTimer?.Start();
    }

    private void SaveSettingsNow()
    {
        _saveTimer?.Stop();
        _settingsStore?.Save(_settings);
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
        if (_widget is null) return;
        if (_widget.IsVisible) _widget.Hide();
        else _widget.Show();
        _tray?.SetWidgetVisible(_widget.IsVisible);
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
