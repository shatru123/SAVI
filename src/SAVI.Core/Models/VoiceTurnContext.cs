namespace SAVI.Core.Models;

public sealed class VoiceTurnContext
{
    public string TurnId { get; init; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public int TurnIndex { get; set; }
    public string UserUtterance { get; set; } = string.Empty;
    public string PartialTranscript { get; set; } = string.Empty;
    public string FinalTranscript { get; set; } = string.Empty;
    public string? AssistantResponse { get; set; }
    public string? VoiceFriendlyResponse { get; set; }
    public string? SpokenUntil { get; set; }
    public bool IsInterrupted { get; set; }
    public bool IsCorrection { get; set; }
    public bool IsSuperseded { get; set; }
    public bool IsDispatched { get; set; }
    public bool AudioPreRollCaptured { get; set; }
    public bool HasPreRollAudio { get; set; }
    public double PreRollDurationMs { get; set; } = 300;
    public double PostRollDurationMs { get; set; } = 150;
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SpeechStartTime { get; set; }
    public DateTimeOffset? AudioStartTime { get; set; }
    public DateTimeOffset? AudioEndTime { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public bool IsCompleted => FinishedAt != null;
    public CancellationTokenSource TurnCts { get; } = new();

    public void MarkInterrupted(string? spokenTextSoFar = null)
    {
        IsInterrupted = true;
        SpokenUntil = spokenTextSoFar;
        FinishedAt = DateTimeOffset.UtcNow;
        try
        {
            if (!TurnCts.IsCancellationRequested)
            {
                TurnCts.Cancel();
            }
        }
        catch (ObjectDisposedException) { }
    }

    public void MarkCompleted(string assistantResponse, string? voiceFriendly = null)
    {
        AssistantResponse = assistantResponse;
        VoiceFriendlyResponse = voiceFriendly ?? assistantResponse;
        SpokenUntil = VoiceFriendlyResponse;
        FinishedAt = DateTimeOffset.UtcNow;
    }
}
