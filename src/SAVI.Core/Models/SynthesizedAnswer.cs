using SAVI.Core.ValueObjects;

namespace SAVI.Core.Models;

public record SynthesizedAnswer
{
    public string MainContent { get; init; } = string.Empty;
    public string VoiceContent { get; init; } = string.Empty;
    public double Confidence { get; init; } = 1.0;
    public bool IsVerified { get; init; } = true;
    public bool HasContradictions { get; init; }
    public string? ContradictionExplanation { get; init; }
    public IReadOnlyList<SourceReference> Sources { get; init; } = Array.Empty<SourceReference>();
    public bool IsComplete { get; init; } = true;
    public IReadOnlyList<string> UnansweredSubQuestions { get; init; } = Array.Empty<string>();
}
