using SAVI.Core.Enums;

namespace SAVI.Core.Models;

public record SelfKnowledgeMatch
{
    public SelfKnowledgeCategory Category { get; init; }
    public string Topic { get; init; } = string.Empty;
    public double Confidence { get; init; } = 1.0;
    public string? SubTopic { get; init; }
}

public record SelfKnowledgeAnswer
{
    public SelfKnowledgeCategory Category { get; init; }
    public string Topic { get; init; } = string.Empty;
    public string TextResponse { get; init; } = string.Empty;
    public string SpeechResponse { get; init; } = string.Empty;
    public double Confidence { get; init; } = 1.0;
    public long ExecutionTimeMs { get; init; }
    public IReadOnlyList<string> KeyFacts { get; init; } = Array.Empty<string>();
}
