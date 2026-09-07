using System.Collections.Concurrent;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Infrastructure.Providers;

public class ProviderRegistry : IProviderRegistry
{
    private readonly ConcurrentDictionary<string, ICapabilityProvider> _providers = new();

    public ProviderRegistry(IEnumerable<ICapabilityProvider> providers)
    {
        foreach (var p in providers)
        {
            Register(p);
        }
    }

    public void Register(ICapabilityProvider provider)
    {
        _providers[provider.Id] = provider;
    }

    public IReadOnlyCollection<ICapabilityProvider> GetAll() => _providers.Values.ToList();

    public IReadOnlyCollection<ICapabilityProvider> GetByCapability(string capability)
    {
        return _providers.Values
            .Where(p => p.Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase))
            .OrderBy(p => p.Priority)
            .ToList();
    }

    public ICapabilityProvider? GetById(string id)
    {
        _providers.TryGetValue(id, out var provider);
        return provider;
    }

    public IReadOnlyList<ICapabilityProvider> RankProviders(TaskRequest request)
    {
        return _providers.Values
            .Where(p => p.CanHandle(request))
            .OrderBy(p => p.Priority)
            .ToList();
    }

    public async Task<IReadOnlyList<ProviderMetadata>> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        var tasks = _providers.Values.Select(async p =>
        {
            var isHealthy = false;
            var sw = global::System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
                isHealthy = await p.HealthCheckAsync(linked.Token);
            }
            catch
            {
                isHealthy = false;
            }
            sw.Stop();

            return new ProviderMetadata
            {
                Id = p.Id,
                Name = p.Name,
                Capability = string.Join(", ", p.Capabilities),
                Priority = p.Priority,
                IsHealthy = isHealthy,
                Latency = sw.Elapsed,
                IsPaid = false,
                RequiresAuth = false,
                ReliabilityScore = isHealthy ? 1.0 : 0.2
            };
        });

        var results = await Task.WhenAll(tasks);
        return results.OrderBy(m => m.Priority).ToList();
    }
}
