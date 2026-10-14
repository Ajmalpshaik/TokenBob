using System.Windows;
using System.Windows.Controls;
using TokenBob.App.Input;
using TokenBob.App.Platform;
using TokenBob.App.Widget;
using TokenBob.Core;
using TokenBob.Core.Alerts;
using TokenBob.Core.Config;
using TokenBob.Core.Logging;
using TokenBob.Core.Screens;

namespace TokenBob.App;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private ILog _log = NullLog.Instance;
    private WidgetWindow? _widget;
    private GestureController? _gestures;
    private WidgetSettings _settings = new();
    private JsonStore<WidgetSettings>? _settingsStore;

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
        _gestures = new GestureController(_widget, _settings, Monitors.GetAll);
        _gestures.SettingsRequested += () => _log.Info("Settings requested");
        _gestures.PullRefreshRequested += () => _log.Info("Pull to refresh");
        _gestures.UsagePageRequested += () =>
        {
            _log.Info("Opening usage page");
            Browser.Open(AppInfo.UsagePageUrl);
        };
        _gestures.PlacementChanged += SavePlacement;
        ShowSampleNumbers(_widget);
        _widget.Ornament.ContextMenu = new ContextMenu { Items = { QuitItem() } };
        _widget.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _log.Info("TokenBob exiting");
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void SavePlacement(PlacementResult placement)
    {
        _settings.Monitor = Placement.IdOf(placement.Monitor);
        _settings.AnchorXDevice = placement.AnchorXDevice;
        _settings.RopeLength = placement.RopeLength;
        _settingsStore?.Save(_settings);
        _log.Info("Placement saved");
    }

    private static void ShowSampleNumbers(WidgetWindow widget)
    {
        widget.Ornament.FiveHourPercent = 42;
        widget.Ornament.WeeklyPercent = 18;
        widget.Ornament.CenterText = "42%";
        widget.Ornament.FiveHourLevel = ColorLevel.Green;
        widget.Ornament.WeeklyLevel = ColorLevel.Green;
    }

    private MenuItem QuitItem()
    {
        var item = new MenuItem { Header = "Quit TokenBob" };
        item.Click += (_, _) => Shutdown();
        return item;
    }
}
