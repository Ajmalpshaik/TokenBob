using System.Text.RegularExpressions;

namespace TokenBob.Core.Usage;

/// <summary>Which pages the Sign-in window may open. Everything else is cancelled.</summary>
public static partial class SignInNavigationPolicy
{
    public const string LoginUrl = "https://claude.ai/login";

    private static readonly string[] AllowedDomains = ["claude.ai", "anthropic.com"];
    private static readonly string[] AllowedExactHosts = ["accounts.youtube.com"];

    public static bool IsAllowed(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps) return false;

        var host = uri.Host.ToLowerInvariant();
        return AllowedExactHosts.Contains(host)
            || GoogleSignInHost().IsMatch(host)
            || AllowedDomains.Any(domain => host == domain || host.EndsWith("." + domain, StringComparison.Ordinal));
    }

    /// <summary>
    /// Google's sign-in pages, including the country sites it visits to finish a login
    /// (e.g. accounts.google.com, www.google.com.qa, accounts.google.co.uk).
    /// </summary>
    [GeneratedRegex(@"^(accounts|www)\.google\.(com|co\.[a-z]{2}|com\.[a-z]{2}|[a-z]{2})$")]
    private static partial Regex GoogleSignInHost();
}
