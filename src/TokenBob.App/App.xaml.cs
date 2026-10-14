using System.Windows;
using System.Windows.Controls;
using TokenBob.App.Platform;
using TokenBob.App.Widget;
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

        var settings = new JsonStore<WidgetSettings>(AppPaths.SettingsFile, _log).Load().Normalized();
        var placement = Placement.Resolve(Monitors.GetAll(), settings.Monitor, settings.AnchorXDevice, settings.RopeLength);

        _widget = new WidgetWindow(settings, placement);
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
