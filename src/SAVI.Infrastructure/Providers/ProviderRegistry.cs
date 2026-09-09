using System.Collections.Concurrent;
using System.Diagnostics;

using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;
using SAVI.Infrastructure.Resilience;

namespace SAVI.Infrastructure.Providers;

public class ProviderRegistry : IProviderRegistry
{
    private readonly ConcurrentDictionary<string, ICapabilityProvider> _providers = new();
    private readonly ProviderScorer _scorer;
    private readonly CircuitBreakerRegistry _circuitBreakers;

    public ProviderRegistry(
        IEnumerable<ICapabilityProvider> providers,
        ProviderScorer? scorer = null,
        CircuitBreakerRegistry? circuitBreakers = null)
    {
        _scorer = scorer ?? new ProviderScorer();
        _circuitBreakers = circuitBreakers ?? new CircuitBreakerRegistry();

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
        var candidates = _providers.Values
            .Where(p => p.CanHandle(request))
            .Where(p => _circuitBreakers.GetRecord(p.Id).GetCurrentState() != CircuitBreakerState.Open)
            .ToList();
        if (candidates.Count <= 1) return candidates;

        return candidates
            .Select(p =>
            {
                var record = _circuitBreakers.GetRecord(p.Id);
                var score = _scorer.Score(p, request, record.GetCurrentState(), record.IsRateLimited());
                return (Provider: p, Score: score);
            })
            .OrderByDescending(x => x.Score.TotalScore)
            .ThenBy(x => x.Provider.Priority)
            .Select(x => x.Provider)
            .ToList();
    }

    public void RecordResult(string providerId, long latencyMs, bool succeeded, bool rateLimited = false, TimeSpan? retryAfter = null)
    {
        var record = _circuitBreakers.GetRecord(providerId);
        if (succeeded)
        {
            record.RecordSuccess(Math.Max(0, latencyMs));
        }
        else
        {
            record.RecordFailure(Math.Max(0, latencyMs), rateLimited, retryAfter);
        }
    }

    public Task<IReadOnlyList<ProviderMetadata>> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        var results = _providers.Values.Select(p =>
        {
            var record = _circuitBreakers.GetRecord(p.Id);
            var state = record.GetCurrentState();
            var isHealthy = state == CircuitBreakerState.Healthy || state == CircuitBreakerState.HalfOpen;
            var latency = record.GetAverageLatency();
            if (latency == TimeSpan.FromMilliseconds(200) && p.TypicalLatency > TimeSpan.Zero)
            {
                latency = p.TypicalLatency;
            }

            return new ProviderMetadata
            {
                Id = p.Id,
                Name = p.Name,
                Capability = string.Join(", ", p.Capabilities),
                Category = p.Category,
                CostType = p.CostType,
                AuthorityLevel = p.AuthorityLevel,
                AccuracyScore = p.AccuracyScore,
                ReliabilityScore = p.ReliabilityScore,
                Priority = p.Priority,
                IsHealthy = isHealthy,
                CircuitState = state,
                Latency = latency,
                TypicalLatency = p.TypicalLatency,
                RollingP95Latency = record.GetP95Latency(),
                RollingSuccessRate = record.GetSuccessRate(),
                ConsecutiveFailures = record.ConsecutiveFailures,
                LastSuccessfulCall = record.LastSuccessTime,
                LastFailure = record.LastFailureTime,
                IsPaid = p.CostType == ProviderCostType.OptionalPaid,
                RequiresAuth = false,
                SupportsVerification = p.SupportsVerification,
                SupportsCaching = p.SupportsCaching,
                Timeout = p.Timeout
            };
        }).OrderBy(m => m.Priority).ToList();

        return Task.FromResult<IReadOnlyList<ProviderMetadata>>(results);
    }

    public async Task RefreshHealthAsync(CancellationToken cancellationToken = default)
    {
        var tasks = _providers.Values.Select(async p =>
        {
            var record = _circuitBreakers.GetRecord(p.Id);
            var sw = Stopwatch.StartNew();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
                var ok = await p.HealthCheckAsync(linked.Token);
                sw.Stop();
                if (ok)
                {
                    record.RecordSuccess(sw.ElapsedMilliseconds);
                }
                else
                {
                    record.RecordFailure(sw.ElapsedMilliseconds);
                }
            }
            catch
            {
                sw.Stop();
                record.RecordFailure(sw.ElapsedMilliseconds);
            }
        });

        await Task.WhenAll(tasks);
    }
}
