using SAVI.Core.Enums;
using SAVI.Core.ValueObjects;

namespace SAVI.Core.Models;

public sealed record AgentResponse
{
    public string Message { get; init; } = string.Empty;
    public string ConversationId { get; init; } = string.Empty;
    public bool Success { get; init; } = true;
    public IReadOnlyList<SourceReference> Sources { get; init; } = Array.Empty<SourceReference>();
    public IReadOnlyList<ToolExecutionResult> ExecutedTools { get; init; } = Array.Empty<ToolExecutionResult>();
    public bool RequiresApproval { get; init; }
    public ActionApprovalRequest? PendingApprovalAction { get; init; }
    public VoiceState ActiveVoiceState { get; init; } = VoiceState.Idle;
    public double Confidence { get; init; } = 1.0;
    public Dictionary<string, string> Telemetry { get; init; } = new();
    public string? TaskId { get; init; }
    public IReadOnlyList<string> ActivityLogs { get; init; } = Array.Empty<string>();
}
