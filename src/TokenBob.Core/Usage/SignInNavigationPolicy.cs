namespace TokenBob.Core.Usage;

/// <summary>Which pages the Sign-in window may open. Everything else is cancelled.</summary>
public static class SignInNavigationPolicy
{
    public const string LoginUrl = "https://claude.ai/login";

    private static readonly string[] AllowedDomains = ["claude.ai", "anthropic.com"];
    private static readonly string[] AllowedExactHosts = ["accounts.google.com"];

    public static bool IsAllowed(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps) return false;

        var host = uri.Host.ToLowerInvariant();
        return AllowedExactHosts.Contains(host)
            || AllowedDomains.Any(domain => host == domain || host.EndsWith("." + domain, StringComparison.Ordinal));
    }
}
