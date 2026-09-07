namespace SAVI.Core.Models;

public sealed record SearchRequest
{
    public string Query { get; init; } = string.Empty;
    public int MaxResults { get; init; } = 5;
    public bool RequireAuthoritative { get; init; }
}
