using SAVI.Core.Enums;

namespace SAVI.Application.DTOs;

public sealed record MemoryItemDto
{
    public string Id { get; init; } = string.Empty;
    public MemoryType Type { get; init; }
    public string Content { get; init; } = string.Empty;
    public double Importance { get; init; }
    public double Confidence { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public string? SourceConversationId { get; init; }
}

public sealed record CreateMemoryDto
{
    public MemoryType Type { get; init; } = MemoryType.Preference;
    public string Content { get; init; } = string.Empty;
    public double Importance { get; init; } = 1.0;
    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed record UpdateMemoryDto
{
    public MemoryType Type { get; init; }
    public string Content { get; init; } = string.Empty;
    public double Importance { get; init; }
}
