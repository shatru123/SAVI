using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Core.Interfaces;

public interface IVerificationEngine
{
    Task<VerificationResult> VerifyAndCompareAsync(
        string query,
        IReadOnlyList<ProviderResult> results,
        CancellationToken cancellationToken = default);
}
