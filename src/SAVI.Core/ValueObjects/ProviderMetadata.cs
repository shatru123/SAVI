namespace SAVI.Core.ValueObjects;

public sealed record ProviderMetadata
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Capability { get; init; } = string.Empty;
    public string Endpoint { get; init; } = string.Empty;
    public bool RequiresAuth { get; init; }
    public bool IsPaid { get; init; }
    public int RateLimitPerMinute { get; init; } = 60;
    public double ReliabilityScore { get; init; } = 1.0;
    public int Priority { get; init; } = 100;
    public bool IsHealthy { get; init; } = true;
    public TimeSpan Latency { get; init; } = TimeSpan.Zero;
    public DateTimeOffset? LastSuccessfulCall { get; init; }
}
