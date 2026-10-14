namespace TokenBob.Core.Usage;

public sealed record WebResponse(int Status, string Body);

/// <summary>
/// Runs a GET against claude.ai inside the signed-in browser session.
/// Throws <see cref="HttpRequestException"/> when the network request fails.
/// </summary>
public interface IWebFetcher
{
    /// <param name="path">Absolute path on claude.ai, e.g. "/api/organizations".</param>
    Task<WebResponse> GetAsync(string path, CancellationToken ct);
}
