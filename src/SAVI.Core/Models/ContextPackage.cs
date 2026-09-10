using SAVI.Core.Entities;

namespace SAVI.Core.Models;

public sealed record ContextPackage
{
    public string CurrentPrompt { get; init; } = string.Empty;
    public IReadOnlyList<Message> RecentMessages { get; init; } = Array.Empty<Message>();
    public string? ConversationSummary { get; init; }
    public IReadOnlyList<MemoryItem> RelevantMemories { get; init; } = Array.Empty<MemoryItem>();
    public IReadOnlyList<Message> RelevantHistoricalMessages { get; init; } = Array.Empty<Message>();
    public string Intent { get; init; } = "GeneralChat";
    public string? ResolvedCoreferenceQuery { get; init; }
    public string? UserName { get; init; }
    public string? UserId { get; init; }
}
