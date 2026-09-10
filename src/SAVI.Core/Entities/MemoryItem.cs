using SAVI.Core.Enums;

namespace SAVI.Core.Entities;

public sealed class MemoryItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public MemoryType Type { get; set; } = MemoryType.ConversationFact;
    public string Content { get; set; } = string.Empty;
    public double Importance { get; set; } = 1.0;
    public double Confidence { get; set; } = 1.0;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? SourceConversationId { get; set; }
    public string? UserId { get; set; }
}
