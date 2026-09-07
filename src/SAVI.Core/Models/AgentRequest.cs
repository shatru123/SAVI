using SAVI.Core.Enums;

namespace SAVI.Core.Models;

public sealed record AgentRequest
{
    public string Message { get; init; } = string.Empty;
    public string ConversationId { get; init; } = string.Empty;
    public PersonalityMode? PersonalityOverride { get; init; }
    public bool VoiceActive { get; init; }
    public string ClientType { get; init; } = "Web";
    public string? ApprovedActionId { get; init; }
    public bool ActionApproved { get; init; }
    public Action<int, string>? OnStepProgress { get; init; }
}
