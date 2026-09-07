using SAVI.Core.ValueObjects;

namespace SAVI.Core.Models;

public sealed record SearchResult
{
    public string Query { get; init; } = string.Empty;
    public int TotalFound { get; init; }
    public IReadOnlyList<SearchItem> Items { get; init; } = Array.Empty<SearchItem>();
    public string SourceEngine { get; init; } = "DuckDuckGo";
}
