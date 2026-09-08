using SAVI.Core.ValueObjects;

namespace SAVI.Core.Models;

public record ProviderEvidence
{
    public string ProviderId { get; init; } = string.Empty;
    public string ProviderName { get; init; } = string.Empty;
    public string Capability { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public IReadOnlyList<string> RelevantPassages { get; init; } = Array.Empty<string>();
    public string SourceUrl { get; init; } = string.Empty;
    public double Confidence { get; init; } = 1.0;
    public double RelevanceScore { get; init; }
    public double AuthorityScore { get; init; }
    public double FreshnessScore { get; init; }
    public double RequirementCoverage { get; init; }
    public bool IsDeterministic { get; init; }
    public bool IsRelevant { get; init; } = true;
    public string? RejectionReason { get; init; }
    public TimeSpan Latency { get; init; } = TimeSpan.Zero;
    public DateTime RetrievedAt { get; init; } = DateTime.UtcNow;
    public IReadOnlyList<SourceReference> Sources { get; init; } = Array.Empty<SourceReference>();
}
