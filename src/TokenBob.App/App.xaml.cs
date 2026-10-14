using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using TokenBob.App.Account;
using TokenBob.App.Input;
using TokenBob.App.Platform;
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
    private Mutex? _singleInstance;
    private ILog _log = NullLog.Instance;
    private WidgetSettings _settings = new();
    private JsonStore<WidgetSettings>? _settingsStore;
    private WidgetWindow? _widget;
    private GestureController? _gestures;
    private UsagePresenter? _presenter;
    private WebView2Fetcher? _fetcher;
    private UsagePoller? _poller;
    private DispatcherTimer? _minuteTimer;
    private SignInWindow? _signIn;
    private bool _signInOfferedThisLaunch;

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
        var placement = Placement.Resolve(Monitors.GetAll(), _settings.Monitor, _settings.AnchorXDevice, _settings.RopeLength);

        _widget = new WidgetWindow(_settings, placement);
        _presenter = new UsagePresenter(_widget);
        _gestures = new GestureController(_widget, _settings, Monitors.GetAll);
        _gestures.SettingsRequested += () => _log.Info("Settings requested");
        _gestures.PullRefreshRequested += OnPullRefresh;
        _gestures.PlacementChanged += SavePlacement;
        _gestures.UsagePageRequested += () =>
        {
            _log.Info("Opening usage page");
            Browser.Open(AppInfo.UsagePageUrl);
        };
        _widget.Ornament.ContextMenu = new ContextMenu { Items = { QuitItem() } };
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
        _minuteTimer?.Stop();
        _poller?.Dispose();
        _fetcher?.Dispose();
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
        _presenter.Show(UsageDisplay.From(_poller.State, DateTimeOffset.Now, TimeZoneInfo.Local, CultureInfo.CurrentCulture));
    }

    private void ShowSignIn()
    {
        if (_signIn is not null)
        {
            _signIn.Activate();
            return;
        }
        _signIn = new SignInWindow(_log);
        _signIn.SignedIn += () => _poller?.RefreshNow();
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
        if (e.Mode == PowerModes.Resume) Dispatcher.BeginInvoke(() => _poller?.RefreshNow());
    }

    private void SavePlacement(PlacementResult placement)
    {
        _settings.Monitor = Placement.IdOf(placement.Monitor);
        _settings.AnchorXDevice = placement.AnchorXDevice;
        _settings.RopeLength = placement.RopeLength;
        _settingsStore?.Save(_settings);
        _log.Info("Placement saved");
    }

    private MenuItem QuitItem()
    {
        var item = new MenuItem { Header = "Quit TokenBob" };
        item.Click += (_, _) => Shutdown();
        return item;
    }
}
