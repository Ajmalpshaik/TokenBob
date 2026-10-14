using System.Net.Http;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using TokenBob.Core.Usage;
using static TokenBob.App.Platform.NativeMethods;

namespace TokenBob.App.Account;

/// <summary>
/// Makes claude.ai requests from a hidden WebView2 that shares the sign-in profile, so
/// they behave exactly like the website. The browser engine is started for a refresh and
/// shut down 10 s after the last request, so it uses no memory between refreshes.
/// Must be used from the UI thread.
/// </summary>
internal sealed class WebView2Fetcher : IWebFetcher, IDisposable
{
    private const string OriginUrl = WebViewFetch.Origin + "/api/organizations";
    private static readonly TimeSpan IdleShutdown = TimeSpan.FromSeconds(10);

    private readonly DispatcherTimer _idle;
    private Window? _host;
    private WebView2? _view;
    private Task<CoreWebView2>? _starting;
    private bool _originReady;

    public WebView2Fetcher()
    {
        _idle = new DispatcherTimer(IdleShutdown, DispatcherPriority.Background, (_, _) => Release(), Dispatcher.CurrentDispatcher)
        {
            IsEnabled = false,
        };
    }

    /// <summary>Raised after the browser engine has been shut down.</summary>
    public event Action? Released;

    public async Task<WebResponse> GetAsync(string path, CancellationToken ct)
    {
        _idle.Stop();
        try
        {
            var core = await EnsureOriginAsync(ct);
            var response = await WebViewFetch.GetAsync(core, path, ct);
            if (!WebViewFetch.LooksLikeJson(response.Body)) _originReady = false;   // e.g. a challenge page: reload next time
#if DEBUG
            // Developer builds only: keep the last usage reply on this PC to check the response shape.
            if (path.EndsWith("/usage", StringComparison.Ordinal))
                System.IO.File.WriteAllText(System.IO.Path.Combine(Core.Config.AppPaths.Root, "debug-usage.json"), response.Body);
#endif
            return response;
        }
        finally
        {
            _idle.Start();
        }
    }

    /// <summary>Signs out of claude.ai in TokenBob's profile only. Claude Desktop is not affected.</summary>
    public async Task SignOutAsync()
    {
        _idle.Stop();
        try
        {
            var core = await EnsureCoreAsync(CancellationToken.None);
            core.CookieManager.DeleteAllCookies();
            _originReady = false;
        }
        finally
        {
            _idle.Start();
        }
    }

    public void Dispose() => Release();

    private void Release()
    {
        _idle.Stop();
        if (_view is null && _host is null) return;
        _view?.Dispose();
        _host?.Close();
        _view = null;
        _host = null;
        _starting = null;
        _originReady = false;
        Released?.Invoke();
    }

    private async Task<CoreWebView2> EnsureOriginAsync(CancellationToken ct)
    {
        var core = await EnsureCoreAsync(ct);
        if (_originReady) return core;

        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        // WebView2 reports 4xx pages as unsuccessful; any HTTP reply still means claude.ai was reached.
        void OnCompleted(object? s, CoreWebView2NavigationCompletedEventArgs e) => done.TrySetResult(e.IsSuccess || e.HttpStatusCode > 0);
        core.NavigationCompleted += OnCompleted;
        try
        {
            core.Navigate(OriginUrl);
            if (!await done.Task.WaitAsync(ct)) throw new HttpRequestException("claude.ai could not be reached.");
        }
        finally
        {
            core.NavigationCompleted -= OnCompleted;
        }
        _originReady = true;
        return core;
    }

    /// <summary>Starts the browser engine once, even if two requests ask at the same time.</summary>
    private Task<CoreWebView2> EnsureCoreAsync(CancellationToken ct)
    {
        if (_view?.CoreWebView2 is { } ready) return Task.FromResult(ready);
        _starting ??= StartCoreAsync();
        return _starting.WaitAsync(ct);
    }

    private async Task<CoreWebView2> StartCoreAsync()
    {
        try
        {
            _view = new WebView2();
            _host = new Window
            {
                Width = 1,
                Height = 1,
                Left = -32000,
                Top = -32000,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ShowActivated = false,
                Content = _view,
                Title = "TokenBob helper",
            };
            _host.SourceInitialized += (s, _) =>
            {
                var hwnd = new WindowInteropHelper((Window)s!).Handle;
                var style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
                SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE));
            };
            _host.Show();

            await _view.EnsureCoreWebView2Async(await WebViewEnvironment.GetAsync());
            return _view.CoreWebView2;
        }
        catch
        {
            // Don't leave a hidden window behind; the next refresh starts over.
            Release();
            throw;
        }
    }
}
