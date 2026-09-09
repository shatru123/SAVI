namespace SAVI.Core.ValueObjects;

public sealed record ProviderResult
{
    public string ProviderId { get; init; } = string.Empty;
    public string ProviderName { get; init; } = string.Empty;
    public string Capability { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<string> RelevantPassages { get; init; } = Array.Empty<string>();
    public bool Success { get; init; }
    public object? Data { get; init; }
    public double Confidence { get; init; } = 1.0;
    public DateTimeOffset RetrievedAt { get; init; } = DateTimeOffset.UtcNow;
    public double AuthorityScore { get; init; } = 0.85;
    public double FreshnessScore { get; init; } = 0.85;
    public bool IsDeterministic { get; init; }
    public TimeSpan Latency { get; init; } = TimeSpan.Zero;
    public IReadOnlyList<SourceReference> Sources { get; init; } = Array.Empty<SourceReference>();
    public string? Error { get; init; }

    public static ProviderResult Succeeded(string providerId, string providerName, object? data, double confidence = 1.0, IReadOnlyList<SourceReference>? sources = null)
    {
        return new ProviderResult
        {
            ProviderId = providerId,
            ProviderName = providerName,
            Success = true,
            Data = data,
            Confidence = confidence,
            RetrievedAt = DateTimeOffset.UtcNow,
            Sources = sources ?? Array.Empty<SourceReference>()
        };
    }

    public static ProviderResult Failed(string providerId, string providerName, string error)
    {
        return new ProviderResult
        {
            ProviderId = providerId,
            ProviderName = providerName,
            Success = false,
            Confidence = 0.0,
            Error = error,
            RetrievedAt = DateTimeOffset.UtcNow
        };
    }
}
