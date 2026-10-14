using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using TokenBob.Core.Logging;
using TokenBob.Core.Usage;

namespace TokenBob.App.Account;

/// <summary>
/// One-time claude.ai sign-in. It can only open Claude's own sign-in pages (and Google's),
/// and closes itself once the session works.
/// </summary>
public partial class SignInWindow : Window
{
    private readonly ILog _log;
    private bool _checking;

    public SignInWindow(ILog log)
    {
        InitializeComponent();
        _log = log;
        Loaded += async (_, _) => await StartAsync();
    }

    /// <summary>Raised once the claude.ai session works.</summary>
    public event Action? SignedIn;

    private async Task StartAsync()
    {
        try
        {
            await View.EnsureCoreWebView2Async(await WebViewEnvironment.GetAsync());
        }
        catch (Exception ex)
        {
            _log.Error("WebView2 could not start", ex);
            MessageBox.Show(this, "TokenBob needs the Microsoft Edge WebView2 Runtime, which comes with Windows 11. Please install it and try again.",
                "TokenBob", MessageBoxButton.OK, MessageBoxImage.Warning);
            Close();
            return;
        }

        var core = View.CoreWebView2;
        core.NavigationStarting += OnNavigationStarting;
        core.NewWindowRequested += OnNewWindowRequested;
        core.NavigationCompleted += async (_, _) => await CheckSignedInAsync();
        // claude.ai changes pages inside the site without a full load, so also check on
        // in-page navigation and every few seconds while the window is open.
        core.HistoryChanged += async (_, _) => await CheckSignedInAsync();
        var poll = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.Background,
            async (_, _) => await CheckSignedInAsync(), Dispatcher);
        Closed += (_, _) => poll.Stop();
        core.Navigate(SignInNavigationPolicy.LoginUrl);
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        var valid = Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri);
        if (valid && SignInNavigationPolicy.IsAllowed(uri!)) return;
        e.Cancel = true;
        _log.Warn($"Sign-in window blocked {(valid ? uri!.Scheme + "://" + uri.Host : "an invalid address")}");
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) && SignInNavigationPolicy.IsAllowed(uri))
            View.CoreWebView2.Navigate(e.Uri);
    }

    private async Task CheckSignedInAsync()
    {
        if (_checking || !Uri.TryCreate(View.CoreWebView2.Source, UriKind.Absolute, out var page)) return;
        var host = page.Host.ToLowerInvariant();
        var onClaude = host == "claude.ai" || host.EndsWith(".claude.ai", StringComparison.Ordinal);
        if (!onClaude || page.AbsolutePath.StartsWith("/login", StringComparison.OrdinalIgnoreCase)) return;

        _checking = true;
        try
        {
            var response = await WebViewFetch.GetAsync(View.CoreWebView2, ClaudeWebUsageProvider.OrganizationsPath, CancellationToken.None);
            if (response.Status == 200 && WebViewFetch.LooksLikeJson(response.Body))
            {
                _log.Info("Signed in to claude.ai");
                SignedIn?.Invoke();
                Close();
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"Sign-in check failed: {ex.GetType().Name}");
        }
        finally
        {
            _checking = false;
        }
    }
}
