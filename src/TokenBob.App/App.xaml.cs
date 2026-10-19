using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using TokenBob.App.Account;
using TokenBob.App.Install;
using TokenBob.App.Input;
using TokenBob.App.Platform;
using TokenBob.App.Settings;
using TokenBob.App.Tray;
using TokenBob.App.Ui;
using TokenBob.App.Widget;
using TokenBob.Core;
using TokenBob.Core.Alerts;
using TokenBob.Core.Config;
using TokenBob.Core.Install;
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
    private bool? _appliedDark;

    private EventWaitHandle? _quitSignal;
    private ClaudeWebUsageProvider? _webProvider;

    private bool SignedIn => _poller?.State is { LastGood: not null, Status: not UsageError.NotSignedIn };

    static App()
    {
        // Anything that escapes before the log or a window exists still leaves a trace.
        AppDomain.CurrentDomain.UnhandledException += (_, args) => ReportCrash(args.ExceptionObject as Exception);
    }

    /// <summary>Writes the crash to %AppData%\TokenBob\logs\crash.txt and shows it, so it can be reported.</summary>
    private static void ReportCrash(Exception? ex)
    {
        try
        {
            System.IO.Directory.CreateDirectory(AppPaths.LogsDir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(AppPaths.LogsDir, "crash.txt"), $"{DateTimeOffset.Now:O}{Environment.NewLine}{ex}");
        }
        catch
        {
            // Nothing more to do: the crash is already on its way.
        }
        try
        {
            MessageBox.Show($"TokenBob crashed.{Environment.NewLine}{Environment.NewLine}{ex?.GetType().Name}: {ex?.Message}{Environment.NewLine}{Environment.NewLine}" +
                            "Details were saved to %AppData%\\TokenBob\\logs\\crash.txt",
                "TokenBob", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            // The UI itself may be what failed.
        }
    }

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
        Theme.Apply(IsDarkFor(new JsonStore<WidgetSettings>(AppPaths.SettingsFile, NullLog.Instance).Load().Normalized().Theme));

        var paths = InstallerService.Paths;
        switch (Installer.Decide(e.Args, Environment.ProcessPath ?? "", paths, InstallerService.IsInstalled, InstallerService.IsSingleFileBuild))
        {
            case LaunchMode.Install or LaunchMode.Update:
                var mode = InstallerService.IsInstalled ? LaunchMode.Update : LaunchMode.Install;
                var window = new InstallWindow(mode, new FileLog(AppPaths.LogsDir, TimeProvider.System));
                window.Closed += (_, _) => Shutdown();
                window.Show();
                return;
            case LaunchMode.Uninstall:
                RunUninstall();
                Shutdown();
                return;
        }

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
        ListenForQuit();
        MoveProfileOutOfRoaming();

        try
        {
            StartWidget();
        }
        catch (Exception ex)
        {
            _log.Error("TokenBob couldn't start", ex);
            MessageBox.Show("TokenBob couldn't start. Details are in %AppData%\\TokenBob\\logs.", "TokenBob",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            Shutdown();
        }
    }

    private void StartWidget()
    {
        _alertStore = new JsonStore<AlertMemory>(AppPaths.AlertStateFile, _log);
        if (!_demo) _alertMemory = _alertStore.Load();

        _settingsStore = new JsonStore<WidgetSettings>(AppPaths.SettingsFile, _log);
        _settings = _settingsStore.Load().Normalized();
        _saveTimer = new DispatcherTimer(SaveDelay, DispatcherPriority.Background, (_, _) => SaveSettingsNow(), Dispatcher) { IsEnabled = false };
        SyncStartup();

        ApplyTheme();
        WindowsTheme.Changed += ApplyTheme;

        var placement = Placement.Resolve(Monitors.GetAll(), _settings.Monitor, _settings.AnchorXDevice, _settings.RopeLength);
        _widget = new WidgetWindow(_settings, placement);
        _widget.DisplayChanged += OnDisplayChanged;
        _widget.Idle += MemoryTrim.Trim;
        _presenter = new UsagePresenter(_widget, _settings);

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
        _webProvider = _demo ? null : new ClaudeWebUsageProvider(_fetcher, TimeProvider.System, _log);
        IUsageProvider provider = _webProvider ?? (IUsageProvider)new DemoUsageProvider();
        _poller = new UsagePoller(provider, TimeProvider.System, TimeSpan.FromMinutes(_settings.RefreshMinutes), _log);
        _poller.Updated += OnUsage;
        _poller.Start();
        if (_demo) StartDemo((DemoUsageProvider)provider);

        _minuteTimer = new DispatcherTimer(TimeSpan.FromSeconds(60), DispatcherPriority.Background, (_, _) => OnMinute(), Dispatcher);
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    private void OnMinute()
    {
        Render();
        // After sleep (or Modern Standby) the refresh timer may not have fired on time.
        if (_poller is { } poller && UsagePoller.IsOverdue(poller.LastAttemptAt, poller.CurrentDelay, DateTimeOffset.UtcNow))
        {
            _log.Info("Refresh was overdue; refreshing now");
            poller.RefreshNow(ignoreCooldown: true);
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(OnDisplayChanged);

    /// <summary>A monitor was plugged in or out, or its resolution or scale changed: re-place the widget.</summary>
    private void OnDisplayChanged()
    {
        if (_widget is null) return;
        var monitors = Monitors.GetAll();
        if (monitors.Count == 0) return;
        var current = _widget.Placement;
        _widget.MoveTo(Placement.Resolve(monitors, Placement.IdOf(current.Monitor), current.AnchorXDevice, current.RopeLength));
    }

    /// <summary>
    /// Versions before 0.2 kept the sign-in profile in roaming %AppData%, where a domain profile
    /// could copy the session off this PC. Move it to %LocalAppData% once; if that fails the user
    /// simply signs in again.
    /// </summary>
    private void MoveProfileOutOfRoaming()
    {
        try
        {
            if (!System.IO.Directory.Exists(AppPaths.LegacyWebView2Dir) || System.IO.Directory.Exists(AppPaths.WebView2Dir)) return;
            System.IO.Directory.CreateDirectory(AppPaths.LocalRoot);
            System.IO.Directory.Move(AppPaths.LegacyWebView2Dir, AppPaths.WebView2Dir);
            _log.Info("Moved the sign-in profile to " + AppPaths.LocalRoot);
        }
        catch (Exception ex)
        {
            _log.Warn("Couldn't move the sign-in profile out of the roaming folder: " + ex.Message);
        }
    }

    /// <summary>An installer or uninstaller sets this event to close TokenBob cleanly.</summary>
    private void ListenForQuit()
    {
        _quitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, InstallerService.QuitEventName);
        var signal = _quitSignal;
        new Thread(() =>
        {
            signal.WaitOne();
            Dispatcher.BeginInvoke(Shutdown);
        }) { IsBackground = true, Name = "TokenBob quit listener" }.Start();
    }

    private static void RunUninstall()
    {
        if (MessageBox.Show("Remove TokenBob from this PC?", "Uninstall TokenBob",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;

        var removeData = MessageBox.Show(
            "Also remove your TokenBob settings and its Claude sign-in from this PC?" + Environment.NewLine + Environment.NewLine +
            "Choose No to keep them in case you install TokenBob again.",
            "Uninstall TokenBob", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

        try
        {
            InstallerService.Uninstall();
        }
        catch (Exception ex)
        {
            MessageBox.Show("TokenBob couldn't be removed: " + ex.Message, "Uninstall TokenBob",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        MessageBox.Show("TokenBob was removed.", "Uninstall TokenBob", MessageBoxButton.OK, MessageBoxImage.Information);
        InstallerService.RemoveFilesAfterExit(removeData);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _log.Info("TokenBob exiting");
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
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
        IReadOnlyList<AlertEvent> events = [];
        if (state is { Status: null, IsLoading: false, LastGood: { } fresh } && !ReferenceEquals(fresh, _lastEvaluated))
        {
            events = AlertRules.Evaluate(_lastEvaluated, fresh, _alertMemory);
            _lastEvaluated = fresh;
            if (events.Count > 0 && !_demo) _alertStore?.Save(_alertMemory);
        }

        // On a reset the old look blasts apart first and the new numbers arrive with the new
        // ornament, so the numbers are shown by the blast instead of right away.
        if (events.Any(e => e is ResetBounce) && _settings.ResetBlast && _widget is { } widget)
        {
            _log.Info("5-hour limit reset: blast");
            widget.PlayReset(Render);
        }
        else
        {
            Render();
        }

        foreach (var alert in events) RunAlert(alert, state.LastGood);

        if (state is { Status: UsageError.NotSignedIn, IsLoading: false } && !_signInOfferedThisLaunch)
        {
            _signInOfferedThisLaunch = true;
            ShowSignIn();
        }
    }

    private void RunAlert(AlertEvent alert, UsageSnapshot? current)
    {
        switch (alert)
        {
            case ThresholdCrossed crossing when _settings.Notifications && current is not null:
                var (title, body) = UsageDisplay.NotificationText(crossing, current, DateTimeOffset.Now, TimeZoneInfo.Local, CultureInfo.CurrentCulture);
                _tray?.Notify(title, body);
                _log.Info($"Notified: {crossing.Limit} {crossing.Threshold}%");
                break;
            case Shiver when _settings.Shiver:
                _widget?.Shiver();
                break;
            case ResetBounce when !_settings.ResetBlast:
                _widget?.Sim.ApplyImpulse(new Vec2(0, -900));
                _widget?.Wake();
                break;
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
        _settingsWindow?.Update(model, SignedIn);
    }

    /// <summary>Light or dark for the widget and every window: the user's choice, or what Windows uses.</summary>
    private void ApplyTheme()
    {
        var dark = IsDarkFor(_settings.Theme);
        if (_appliedDark == dark) return;
        _appliedDark = dark;
        Palette.UseDark(dark);
        Theme.Apply(dark);
        _widget?.Repaint();
        Render();
    }

    private static bool IsDarkFor(AppTheme theme) => theme switch
    {
        AppTheme.Dark => true,
        AppTheme.Light => false,
        _ => WindowsTheme.IsDark,
    };

    /// <summary>Hangs the widget back at the default spot on the main monitor (it may have been lost off-screen).</summary>
    private void ResetPlacement()
    {
        if (_widget is null) return;
        _settings.Monitor = null;
        _settings.AnchorXDevice = null;
        var placement = Placement.Resolve(Monitors.GetAll(), null, null, _settings.RopeLength);
        _widget.MoveTo(placement);
        SavePlacement(placement);
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
        _settingsWindow.PlayResetRequested += () => _widget?.PlayReset(() => { });
        _settingsWindow.ResetPositionRequested += ResetPlacement;
        _settingsWindow.QuitRequested += Shutdown;
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        Render();
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OnSettingsChanged()
    {
        ApplyTheme();
        _widget?.ApplySettings();
        Render();   // e.g. the number shown in the ring
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
            if (Installer.OwnsStartupEntry(Environment.ProcessPath ?? "", InstallerService.Paths)) StartupRegistration.Refresh();
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
        _signInOfferedThisLaunch = true;   // the user chose this; don't pop the sign-in window back up
        _webProvider?.ForgetOrganization();
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
        _signIn.SignedIn += () =>
        {
            _webProvider?.ForgetOrganization();   // it may be a different account
            _poller?.RefreshNow(ignoreCooldown: true);
        };
        _signIn.Closed += (_, _) => _signIn = null;
        _signIn.Show();
    }

    private void OnPullRefresh()
    {
        if (_widget is null) return;
        // The lamp-chain click always plays; the refresh itself is what the 30 s cooldown limits.
        _widget.Sim.ApplyImpulse(new Vec2(0, -600));
        _widget.Wake();
        if (_poller?.RefreshNow() == true) _log.Info("Pull to refresh");
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
