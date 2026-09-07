using SAVI.Core.ValueObjects;

namespace SAVI.Core.Interfaces;

public interface IProviderCache
{
    Task<ProviderResult?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, ProviderResult result, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task<ProviderResult> GetOrExecuteAsync(
        string providerId,
        string cacheKey,
        TimeSpan ttl,
        Func<CancellationToken, Task<ProviderResult>> factory,
        CancellationToken cancellationToken = default);

    long CacheHits { get; }
    long CacheMisses { get; }
    double HitRatio { get; }
}
