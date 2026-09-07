namespace SAVI.Core.Models;

public sealed record VoiceTurnResult
{
    public string TurnId { get; init; } = string.Empty;
    public string UserUtterance { get; init; } = string.Empty;
    public string AssistantResponse { get; init; } = string.Empty;
    public string VoiceFriendlyResponse { get; init; } = string.Empty;
    public bool Success { get; init; } = true;
    public bool WasInterrupted { get; init; }
    public string? SpokenUntil { get; init; }
    public double Confidence { get; init; } = 1.0;
    public IReadOnlyList<string> SentenceChunks { get; init; } = Array.Empty<string>();
    public Dictionary<string, double> LatencyMetricsMs { get; init; } = new();
}
