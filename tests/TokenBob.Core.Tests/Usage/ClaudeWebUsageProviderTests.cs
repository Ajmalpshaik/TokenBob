using Microsoft.Extensions.Time.Testing;
using TokenBob.Core.Tests.TestDoubles;
using TokenBob.Core.Usage;

namespace TokenBob.Core.Tests.Usage;

public class ClaudeWebUsageProviderTests
{
    private const string Orgs = "[{\"uuid\":\"org-1\",\"capabilities\":[\"chat\"]}]";
    private const string UsageBody =
        "{\"five_hour\":{\"utilization\":42.0,\"resets_at\":\"2026-10-14T12:10:00Z\"}," +
        "\"seven_day\":{\"utilization\":18.0,\"resets_at\":\"2026-10-16T09:00:00Z\"},\"seven_day_opus\":null}";
    private const string UsagePath = "/api/organizations/org-1/usage";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero));
    private readonly RecordingLog _log = new();

    private ClaudeWebUsageProvider Provider(FakeFetcher fetcher) => new(fetcher, _time, _log);

    private static FakeFetcher Fetcher(int usageStatus = 200, string usageBody = UsageBody) =>
        new FakeFetcher().Respond("/api/organizations", 200, Orgs).Respond(UsagePath, usageStatus, usageBody);

    [Fact]
    public async Task Valid_response_maps_to_snapshot()
    {
        var fetcher = Fetcher();
        var result = await Provider(fetcher).FetchAsync(CancellationToken.None);

        Assert.Null(result.Error);
        var s = result.Snapshot!;
        Assert.Equal(new LimitUsage(42, DateTimeOffset.Parse("2026-10-14T12:10:00Z")), s.FiveHour);
        Assert.Equal(new LimitUsage(18, DateTimeOffset.Parse("2026-10-16T09:00:00Z")), s.Weekly);
        Assert.Equal(_time.GetUtcNow(), s.FetchedAt);
        Assert.Equal(new[] { "/api/organizations", UsagePath }, fetcher.Paths);
    }

    [Fact]
    public async Task Success_is_logged_without_numbers()
    {
        await Provider(Fetcher()).FetchAsync(CancellationToken.None);
        Assert.Contains("INFO Usage updated", _log.All);
        Assert.DoesNotContain("42", _log.All);
    }

    [Fact]
    public async Task Picks_first_chat_capable_organization()
    {
        var fetcher = new FakeFetcher()
            .Respond("/api/organizations", 200,
                "[{\"uuid\":\"api-org\",\"capabilities\":[\"api\"]},{\"uuid\":\"org-2\",\"capabilities\":[\"chat\",\"claude_max\"]}]")
            .Respond("/api/organizations/org-2/usage", 200, UsageBody);

        var result = await Provider(fetcher).FetchAsync(CancellationToken.None);

        Assert.NotNull(result.Snapshot);
        Assert.Equal("/api/organizations/org-2/usage", fetcher.Paths[^1]);
    }

    [Fact]
    public async Task Falls_back_to_first_organization_without_chat()
    {
        var fetcher = new FakeFetcher()
            .Respond("/api/organizations", 200,
                "[{\"uuid\":\"first\",\"capabilities\":[\"api\"]},{\"uuid\":\"second\",\"capabilities\":[]}]")
            .Respond("/api/organizations/first/usage", 200, UsageBody);

        var result = await Provider(fetcher).FetchAsync(CancellationToken.None);

        Assert.NotNull(result.Snapshot);
        Assert.Equal("/api/organizations/first/usage", fetcher.Paths[^1]);
    }

    [Fact]
    public async Task Organization_is_cached_between_fetches()
    {
        var fetcher = Fetcher();
        var provider = Provider(fetcher);

        await provider.FetchAsync(CancellationToken.None);
        await provider.FetchAsync(CancellationToken.None);

        Assert.Equal(1, fetcher.Paths.Count(p => p == "/api/organizations"));
    }

    [Fact]
    public async Task Usage_404_refetches_organizations_next_time()
    {
        var fetcher = Fetcher(usageStatus: 404, usageBody: "{\"error\":\"not found\"}");
        var provider = Provider(fetcher);

        var first = await provider.FetchAsync(CancellationToken.None);
        fetcher.Respond(UsagePath, 200, UsageBody);
        var second = await provider.FetchAsync(CancellationToken.None);

        Assert.Equal(UsageError.Unreadable, first.Error);
        Assert.NotNull(second.Snapshot);
        Assert.Equal(2, fetcher.Paths.Count(p => p == "/api/organizations"));
    }

    [Fact]
    public async Task Null_limit_or_reset_is_zero_not_error()
    {
        var fetcher = Fetcher(usageBody: "{\"five_hour\":null,\"seven_day\":{\"utilization\":12,\"resets_at\":null}}");

        var result = await Provider(fetcher).FetchAsync(CancellationToken.None);

        Assert.Equal(new LimitUsage(0, null), result.Snapshot!.FiveHour);
        Assert.Equal(new LimitUsage(12, null), result.Snapshot.Weekly);
    }

    [Theory]
    [InlineData(200, "{\"five_hour\":{\"utilization\":1,\"resets_at\":null}}")]
    [InlineData(200, "<!DOCTYPE html><html><body>Just a moment...</body></html>")]
    [InlineData(403, "<!DOCTYPE html><html><body>Checking your browser</body></html>")]
    [InlineData(500, "{\"error\":\"internal\"}")]
    [InlineData(200, "not json at all")]
    public async Task Bad_or_html_responses_are_Unreadable(int status, string body)
    {
        var result = await Provider(Fetcher(status, body)).FetchAsync(CancellationToken.None);
        Assert.Equal(UsageError.Unreadable, result.Error);
    }

    [Fact]
    public async Task Empty_organization_list_is_Unreadable()
    {
        var fetcher = new FakeFetcher().Respond("/api/organizations", 200, "[]");
        var result = await Provider(fetcher).FetchAsync(CancellationToken.None);
        Assert.Equal(UsageError.Unreadable, result.Error);
    }

    [Theory]
    [InlineData(401, "{\"type\":\"error\",\"error\":{\"type\":\"authentication_error\"}}")]
    [InlineData(403, "{\"type\":\"error\",\"error\":{\"type\":\"permission_error\"}}")]
    public async Task Signed_out_responses_are_NotSignedIn(int status, string body)
    {
        var fetcher = new FakeFetcher().Respond("/api/organizations", status, body);
        var result = await Provider(fetcher).FetchAsync(CancellationToken.None);
        Assert.Equal(UsageError.NotSignedIn, result.Error);
    }

    [Fact]
    public async Task Http_429_is_RateLimited()
    {
        var result = await Provider(Fetcher(429, "{\"error\":\"rate_limited\"}")).FetchAsync(CancellationToken.None);
        Assert.Equal(UsageError.RateLimited, result.Error);
    }

    [Fact]
    public async Task Network_failure_is_Offline()
    {
        var fetcher = new FakeFetcher().Throw("/api/organizations", new HttpRequestException("no network"));
        var result = await Provider(fetcher).FetchAsync(CancellationToken.None);
        Assert.Equal(UsageError.Offline, result.Error);
    }

    [Fact]
    public async Task Timeout_after_10_seconds_is_Offline()
    {
        var fetcher = new FakeFetcher().Hang("/api/organizations");

        var pending = Provider(fetcher).FetchAsync(CancellationToken.None);
        Assert.False(pending.IsCompleted);
        _time.Advance(TimeSpan.FromSeconds(10));
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(UsageError.Offline, result.Error);
    }

    [Fact]
    public async Task Log_never_contains_org_id_or_bodies()
    {
        var cases = new[]
        {
            Fetcher(500, "{\"utilization\":\"secret\"}"),
            Fetcher(200, "<html>org-1</html>"),
            Fetcher(429, "{}"),
            Fetcher(404, "{}"),
            new FakeFetcher().Respond("/api/organizations", 401, "{\"utilization\":1}"),
            new FakeFetcher().Throw("/api/organizations", new HttpRequestException("boom")),
        };
        foreach (var fetcher in cases) await Provider(fetcher).FetchAsync(CancellationToken.None);

        Assert.NotEmpty(_log.Messages);
        Assert.DoesNotContain("org-1", _log.All);
        Assert.DoesNotContain("utilization", _log.All);
    }
}
