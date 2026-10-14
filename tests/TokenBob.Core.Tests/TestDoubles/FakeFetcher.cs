using TokenBob.Core.Usage;

namespace TokenBob.Core.Tests.TestDoubles;

/// <summary>Answers claude.ai paths from canned responses and records what was asked.</summary>
public sealed class FakeFetcher : IWebFetcher
{
    private readonly Dictionary<string, Func<CancellationToken, Task<WebResponse>>> _routes = new();

    public List<string> Paths { get; } = new();

    public FakeFetcher Respond(string path, int status, string body)
    {
        _routes[path] = _ => Task.FromResult(new WebResponse(status, body));
        return this;
    }

    public FakeFetcher Throw(string path, Exception exception)
    {
        _routes[path] = _ => Task.FromException<WebResponse>(exception);
        return this;
    }

    public FakeFetcher Hang(string path)
    {
        _routes[path] = async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        };
        return this;
    }

    public Task<WebResponse> GetAsync(string path, CancellationToken ct)
    {
        Paths.Add(path);
        return _routes.TryGetValue(path, out var route)
            ? route(ct)
            : Task.FromResult(new WebResponse(404, "{\"error\":\"not found\"}"));
    }
}
