using System.Globalization;
using System.Text.Json;
using TokenBob.Core.Logging;

namespace TokenBob.Core.Usage;

/// <summary>
/// Reads plan usage from claude.ai's own (unofficial) endpoints. This is the only
/// class that knows those paths and response shapes.
/// </summary>
public sealed class ClaudeWebUsageProvider : IUsageProvider
{
    public const string OrganizationsPath = "/api/organizations";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly IWebFetcher _fetcher;
    private readonly TimeProvider _time;
    private readonly ILog _log;
    private string? _organizationId;

    public ClaudeWebUsageProvider(IWebFetcher fetcher, TimeProvider time, ILog log)
    {
        _fetcher = fetcher;
        _time = time;
        _log = log;
    }

    public static string UsagePath(string organizationId) => $"/api/organizations/{organizationId}/usage";

    /// <summary>Looks the organization up again on the next fetch (after signing in or out).</summary>
    public void ForgetOrganization() => _organizationId = null;

    public async Task<UsageResult> FetchAsync(CancellationToken ct)
    {
        using var timeout = new CancellationTokenSource(Timeout, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        try
        {
            if (_organizationId is null)
            {
                var (organizationId, error) = await ResolveOrganizationAsync(linked.Token);
                if (error is { } e) return UsageResult.Fail(e);
                _organizationId = organizationId;
            }

            var response = await _fetcher.GetAsync(UsagePath(_organizationId!), linked.Token);
            if (response.Status == 404)
            {
                _organizationId = null;
                _log.Warn("Usage request returned HTTP 404; organization will be looked up again");
                return UsageResult.Fail(UsageError.Unreadable);
            }

            using var document = ParseJson(response.Body);
            if (Classify("usage", response.Status, document) is { } usageError)
            {
                // Signed out, or now a different account: find the organization again next time.
                if (usageError == UsageError.NotSignedIn) _organizationId = null;
                return UsageResult.Fail(usageError);
            }

            var snapshot = ReadSnapshot(document!.RootElement);
            if (snapshot is null)
            {
                _log.Warn("Usage response did not have the expected five_hour/seven_day shape");
                return UsageResult.Fail(UsageError.Unreadable);
            }
            _log.Info("Usage updated");
            return UsageResult.Ok(snapshot);
        }
        catch (HttpRequestException ex)
        {
            _log.Warn($"Usage request failed: {ex.GetType().Name}");
            return UsageResult.Fail(UsageError.Offline);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log.Warn("Usage request timed out");
            return UsageResult.Fail(UsageError.Offline);
        }
    }

    private async Task<(string? Id, UsageError? Error)> ResolveOrganizationAsync(CancellationToken ct)
    {
        var response = await _fetcher.GetAsync(OrganizationsPath, ct);
        using var document = ParseJson(response.Body);
        if (Classify("organizations", response.Status, document) is { } error) return (null, error);

        var root = document!.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
        {
            _log.Warn("Organizations response was empty or not a list");
            return (null, UsageError.Unreadable);
        }

        var chosen = root.EnumerateArray().FirstOrDefault(HasChatCapability);
        if (chosen.ValueKind == JsonValueKind.Undefined) chosen = root[0];

        if (chosen.ValueKind == JsonValueKind.Object
            && chosen.TryGetProperty("uuid", out var uuid)
            && uuid.ValueKind == JsonValueKind.String
            && !string.IsNullOrEmpty(uuid.GetString()))
        {
            return (uuid.GetString(), null);
        }

        _log.Warn("Organization entry had no uuid");
        return (null, UsageError.Unreadable);
    }

    private UsageError? Classify(string request, int status, JsonDocument? document)
    {
        if (status == 200 && document is not null) return null;

        _log.Warn($"{request} request returned HTTP {status}{(document is null ? " (not JSON)" : "")}");
        return status switch
        {
            401 => UsageError.NotSignedIn,
            403 when document is not null => UsageError.NotSignedIn,
            429 => UsageError.RateLimited,
            _ => UsageError.Unreadable,
        };
    }

    private UsageSnapshot? ReadSnapshot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("five_hour", out var fiveHour)
            || !root.TryGetProperty("seven_day", out var weekly))
        {
            return null;
        }

        var five = ReadLimit(fiveHour);
        var week = ReadLimit(weekly);
        return five is null || week is null ? null : new UsageSnapshot(five, week, _time.GetUtcNow());
    }

    private static LimitUsage? ReadLimit(JsonElement limit)
    {
        if (limit.ValueKind == JsonValueKind.Null) return new LimitUsage(0, null);
        if (limit.ValueKind != JsonValueKind.Object || !limit.TryGetProperty("utilization", out var utilization)) return null;
        double used;
        switch (utilization.ValueKind)
        {
            case JsonValueKind.Number: used = utilization.GetDouble(); break;
            case JsonValueKind.Null: used = 0; break;   // no activity in this window yet
            default: return null;
        }

        DateTimeOffset? resetsAt = null;
        if (limit.TryGetProperty("resets_at", out var reset) && reset.ValueKind == JsonValueKind.String)
        {
            if (!DateTimeOffset.TryParse(reset.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var parsed))
            {
                return null;
            }
            resetsAt = parsed;
        }

        return new LimitUsage(used, resetsAt);
    }

    private static bool HasChatCapability(JsonElement organization) =>
        organization.ValueKind == JsonValueKind.Object
        && organization.TryGetProperty("capabilities", out var capabilities)
        && capabilities.ValueKind == JsonValueKind.Array
        && capabilities.EnumerateArray().Any(c => c.ValueKind == JsonValueKind.String && c.GetString() == "chat");

    private static JsonDocument? ParseJson(string body)
    {
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
