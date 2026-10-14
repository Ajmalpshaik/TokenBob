using Microsoft.Web.WebView2.Core;
using TokenBob.Core.Config;

namespace TokenBob.App.Account;

/// <summary>
/// The one WebView2 profile TokenBob uses (%AppData%\TokenBob\WebView2). The sign-in
/// window and the hidden fetcher share it, so they share the claude.ai session.
/// </summary>
internal static class WebViewEnvironment
{
    private static Task<CoreWebView2Environment>? _environment;

    public static Task<CoreWebView2Environment> GetAsync() =>
        _environment ??= CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, userDataFolder: AppPaths.WebView2Dir);
}
