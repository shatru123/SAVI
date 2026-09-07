using SAVI.Core.Enums;
using SAVI.Core.ValueObjects;

namespace SAVI.Application.DTOs;

public sealed record SendChatMessageRequest
{
    public string Message { get; init; } = string.Empty;
    public string? ConversationId { get; init; }
    public PersonalityMode? PersonalityOverride { get; init; }
    public bool VoiceActive { get; init; }
    public string? ApprovedActionId { get; init; }
    public bool ActionApproved { get; init; }
}

public sealed record ChatResponseDto
{
    public string ConversationId { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public bool Success { get; init; } = true;
    public IReadOnlyList<SourceReference> Sources { get; init; } = Array.Empty<SourceReference>();
    public bool RequiresApproval { get; init; }
    public string? ApprovalActionId { get; init; }
    public string? ApprovalDescription { get; init; }
    public VoiceState VoiceState { get; init; } = VoiceState.Idle;
    public double Confidence { get; init; } = 1.0;
    public IReadOnlyList<string> ActivityLogs { get; init; } = Array.Empty<string>();
    public string? TaskId { get; init; }
}

public sealed record StreamTokenDto
{
    public string ConversationId { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public bool IsComplete { get; init; }
    public VoiceState VoiceState { get; init; } = VoiceState.Speaking;
}
