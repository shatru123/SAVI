using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using SAVI.Core.Interfaces;
using SAVI.Core.ValueObjects;

namespace SAVI.Infrastructure.Caching;

public class ProviderCache : IProviderCache
{
    private readonly IMemoryCache _memoryCache;
    private readonly ConcurrentDictionary<string, Task<ProviderResult>> _inFlight = new();
    private long _hits;
    private long _misses;

    public ProviderCache(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public long CacheHits => Interlocked.Read(ref _hits);
    public long CacheMisses => Interlocked.Read(ref _misses);
    public double HitRatio
    {
        get
        {
            var total = CacheHits + CacheMisses;
            return total == 0 ? 0.0 : (double)CacheHits / total;
        }
    }

    public Task<ProviderResult?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(key, out ProviderResult? cached))
        {
            Interlocked.Increment(ref _hits);
            return Task.FromResult(cached);
        }

        Interlocked.Increment(ref _misses);
        return Task.FromResult<ProviderResult?>(null);
    }

    public Task SetAsync(string key, ProviderResult result, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        _memoryCache.Set(key, result, ttl);
        return Task.CompletedTask;
    }

    public async Task<ProviderResult> GetOrExecuteAsync(
        string providerId,
        string cacheKey,
        TimeSpan ttl,
        Func<CancellationToken, Task<ProviderResult>> factory,
        CancellationToken cancellationToken = default)
    {
        // 1. Fast Cache Check
        if (_memoryCache.TryGetValue(cacheKey, out ProviderResult? cached) && cached != null)
        {
            Interlocked.Increment(ref _hits);
            return cached;
        }

        Interlocked.Increment(ref _misses);

        // 2. Request Coalescing / Deduplication
        // If multiple callers request the same key concurrently, only run factory once
        Task<ProviderResult> task;
        task = _inFlight.GetOrAdd(cacheKey, key => Task.Run(async () =>
        {
            try
            {
                var res = await factory(cancellationToken);
                if (res.Success && ttl > TimeSpan.Zero)
                {
                    _memoryCache.Set(key, res, ttl);
                }
                return res;
            }
            finally
            {
                _inFlight.TryRemove(key, out _);
            }
        }, cancellationToken));

        return await task;
    }
}
