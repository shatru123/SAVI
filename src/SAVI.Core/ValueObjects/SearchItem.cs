namespace SAVI.Core.ValueObjects;

public sealed record SearchItem
{
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Snippet { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public DateTimeOffset? PublishedAt { get; init; }
    public DateTimeOffset RetrievedAt { get; init; } = DateTimeOffset.UtcNow;
    public double RelevanceScore { get; init; } = 1.0;
}
