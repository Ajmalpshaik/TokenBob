namespace TokenBob.Core.Usage;

public interface IUsageProvider
{
    Task<UsageResult> FetchAsync(CancellationToken ct);
}
