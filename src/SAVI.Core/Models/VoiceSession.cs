using SAVI.Core.Enums;

namespace SAVI.Core.Models;

public sealed class VoiceSession
{
    public string SessionId { get; init; } = Guid.NewGuid().ToString();
    public string ConversationId { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EndedAt { get; set; }
    public VoiceState CurrentState { get; set; } = VoiceState.Idle;
    public string? InputDevice { get; set; }
    public string? OutputDevice { get; set; }
    public int TurnsCount { get; set; }
    public int InterruptionCount { get; set; }
    public string? LastUserUtterance { get; set; }
    public string? LastAssistantResponse { get; set; }
    public string? LastVoiceFriendlyResponse { get; set; }
    public string? LastDetectedCapability { get; set; }
    public string? LastLocation { get; set; }

    public string? ActiveTurnId { get; set; }
    public string? InterruptedResponse { get; set; }
    public string? InterruptedTopic { get; set; }
    public string? InterruptedIntent { get; set; }
    public Dictionary<string, string> InterruptedParameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
