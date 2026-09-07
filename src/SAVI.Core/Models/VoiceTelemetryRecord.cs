namespace SAVI.Core.Models;

public sealed record VoiceTelemetryRecord
{
    public string SessionId { get; init; } = string.Empty;
    public string TurnId { get; init; } = string.Empty;
    public double SttPartialLatencyMs { get; init; }
    public double SttFinalLatencyMs { get; init; }
    public double VadLatencyMs { get; init; }
    public double IntentLatencyMs { get; init; }
    public double ProviderLatencyMs { get; init; }
    public double TtsStartLatencyMs { get; init; }
    public double FirstAudioLatencyMs { get; init; }
    public double InterruptionDetectionLatencyMs { get; init; }
    public double AudioStopLatencyMs { get; init; }
    public double TotalInterruptionLatencyMs { get; init; }
    public double TotalTurnDurationMs { get; init; }
    public bool InterruptedTurn { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
