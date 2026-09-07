using SAVI.Core.Enums;

namespace SAVI.Core.ValueObjects;

public sealed record ProviderMetadata
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Capability { get; init; } = string.Empty;
    public string Endpoint { get; init; } = string.Empty;
    public ProviderCategory Category { get; init; } = ProviderCategory.SpecializedPublicApi;
    public ProviderCostType CostType { get; init; } = ProviderCostType.FreePublic;
    public bool RequiresAuth { get; init; }
    public bool RequiresApiKey { get; init; }
    public bool IsPaid { get; init; }
    public int RateLimitPerMinute { get; init; } = 60;
    public double AuthorityLevel { get; init; } = 0.85;
    public double AccuracyScore { get; init; } = 0.90;
    public double ReliabilityScore { get; init; } = 1.0;
    public double Freshness { get; init; } = 0.90;
    public int Priority { get; init; } = 100;
    public bool IsHealthy { get; init; } = true;
    public CircuitBreakerState CircuitState { get; init; } = CircuitBreakerState.Healthy;
    public TimeSpan Latency { get; init; } = TimeSpan.Zero;
    public TimeSpan TypicalLatency { get; init; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan RollingP95Latency { get; init; } = TimeSpan.Zero;
    public double RollingSuccessRate { get; init; } = 1.0;
    public int ConsecutiveFailures { get; init; } = 0;
    public DateTimeOffset? LastSuccessfulCall { get; init; }
    public DateTimeOffset? LastFailure { get; init; }
    public bool SupportsVerification { get; init; } = true;
    public bool SupportsCaching { get; init; } = true;
    public TimeSpan DefaultCacheTtl { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(3);
}
