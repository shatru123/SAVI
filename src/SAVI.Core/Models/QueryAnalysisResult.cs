using SAVI.Core.Enums;

namespace SAVI.Core.Models;

public record QueryAnalysisResult
{
    public string RawPrompt { get; init; } = string.Empty;
    public string CleanPrompt { get; init; } = string.Empty;
    public string Topic { get; init; } = string.Empty;
    public string Intent { get; init; } = string.Empty;
    public string Domain { get; init; } = "General";
    public IReadOnlyList<string> Entities { get; init; } = Array.Empty<string>();
    public bool IsTechnical { get; init; }
    public IReadOnlyList<string> SubQuestions { get; init; } = Array.Empty<string>();
    public string? ResolvedContextQuery { get; init; }
    public string CanonicalLookupQuery { get; init; } = string.Empty;
    public double Confidence { get; init; } = 0.90;
    public VerificationPolicy? PolicyOverride { get; init; }
}
