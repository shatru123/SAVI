using SAVI.Core.ValueObjects;

namespace SAVI.Core.Models;

public sealed record VerificationResult
{
    public bool IsVerified { get; init; }
    public double Confidence { get; init; }
    public bool HasContradictions { get; init; }
    public string Synthesis { get; init; } = string.Empty;
    public IReadOnlyList<SourceReference> Sources { get; init; } = Array.Empty<SourceReference>();
    public string? ContradictionExplanation { get; init; }
}
