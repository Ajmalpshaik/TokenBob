using System.Windows;
using TokenBob.Core.Install;
using TokenBob.Core.Logging;

namespace TokenBob.App.Install;

/// <summary>The one-click install (or update) window shown when TokenBob.exe is double-clicked.</summary>
public partial class InstallWindow : Window
{
    private readonly ILog _log;

    internal InstallWindow(LaunchMode mode, ILog log)
    {
        InitializeComponent();
        _log = log;

        var version = InstallerService.Version;
        Title = Heading.Text = mode == LaunchMode.Update ? $"Update TokenBob to {version}" : $"Install TokenBob {version}";
        InstallButton.Content = mode == LaunchMode.Update ? "Update" : "Install";
        if (mode == LaunchMode.Update) StartWithWindows.IsChecked = Platform.StartupRegistration.IsEnabled();

        Cancel.Click += (_, _) => Close();
        InstallButton.Click += async (_, _) => await InstallAsync();
    }

    private async Task InstallAsync()
    {
        InstallButton.IsEnabled = Cancel.IsEnabled = StartWithWindows.IsEnabled = false;
        Status.Text = "Installing…";
        var startWithWindows = StartWithWindows.IsChecked == true;
        try
        {
            await Task.Run(() => InstallerService.Install(startWithWindows));
            _log.Info($"Installed TokenBob {InstallerService.Version}");
            Close();
        }
        catch (Exception ex)
        {
            _log.Error("Install failed", ex);
            Status.Text = "Couldn't install TokenBob. Close any open TokenBob and try again.";
            InstallButton.IsEnabled = Cancel.IsEnabled = StartWithWindows.IsEnabled = true;
        }
    }
}
