using SAVI.Core.Enums;
using SAVI.Core.Models;

namespace SAVI.Core.Interfaces;

public interface IVoiceConversationSession
{
    string SessionId { get; }
    string ConversationId { get; }
    VoiceState CurrentState { get; }
    VoiceTurnContext? CurrentTurn { get; }
    bool IsActive { get; }

    Task StartAsync(string conversationId, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task InterruptAsync(CancellationToken cancellationToken = default);
    Task PauseAsync(CancellationToken cancellationToken = default);
    Task ResumeAsync(CancellationToken cancellationToken = default);
    Task<VoiceTurnResult> ProcessUtteranceAsync(string text, bool isInterruption = false, CancellationToken cancellationToken = default);

    void NotifyUserSpeechStarted(string? turnId = null);
    void NotifyUserSpeechPartial(string partialText, string? turnId = null);
    void NotifyUserSpeechStopped(string? turnId = null);

    event Action<VoiceState>? StateChanged;
    event Action<string, object?>? VoiceEventEmitted;
    event Action<VoiceTelemetryRecord>? TelemetryRecorded;
}
