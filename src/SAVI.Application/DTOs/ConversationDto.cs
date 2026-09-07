using SAVI.Core.Enums;
using SAVI.Core.ValueObjects;

namespace SAVI.Application.DTOs;

public sealed record ConversationSummaryDto
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public bool IsArchived { get; init; }
    public int MessageCount { get; init; }
    public string? Summary { get; init; }
}

public sealed record MessageDto
{
    public string Id { get; init; } = string.Empty;
    public MessageRole Role { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
    public MessageType MessageType { get; init; }
    public IReadOnlyList<SourceReference>? Sources { get; init; }
    public IReadOnlyList<ToolExecutionResult>? ToolExecutions { get; init; }
}

public sealed record ConversationDetailDto
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public bool IsArchived { get; init; }
    public string? Summary { get; init; }
    public IReadOnlyList<MessageDto> Messages { get; init; } = Array.Empty<MessageDto>();
}

public sealed record RenameConversationDto
{
    public string Title { get; init; } = string.Empty;
}
