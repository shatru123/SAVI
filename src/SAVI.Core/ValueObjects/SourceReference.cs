namespace SAVI.Core.ValueObjects;

public sealed record SourceReference
{
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Snippet { get; init; } = string.Empty;
    public string SourceName { get; init; } = string.Empty;
    public DateTimeOffset RetrievedAt { get; init; } = DateTimeOffset.UtcNow;
    public double ReliabilityScore { get; init; } = 1.0;
}
