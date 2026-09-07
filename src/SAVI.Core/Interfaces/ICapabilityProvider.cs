using SAVI.Core.Enums;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Core.Interfaces;

public interface ICapabilityProvider
{
    string Id { get; }
    string Name { get; }
    IReadOnlyCollection<string> Capabilities { get; }
    int Priority { get; }
    ProviderCategory Category => ProviderCategory.SpecializedPublicApi;
    ProviderCostType CostType => ProviderCostType.FreePublic;
    double AuthorityLevel => 0.85;
    double AccuracyScore => 0.90;
    double ReliabilityScore => 1.0;
    TimeSpan TypicalLatency => TimeSpan.FromMilliseconds(500);
    TimeSpan Timeout => TimeSpan.FromSeconds(3);
    bool SupportsCaching => true;
    TimeSpan CacheTtl => TimeSpan.FromMinutes(5);
    bool SupportsVerification => true;

    bool CanHandle(TaskRequest request);
    Task<ProviderResult> ExecuteAsync(TaskRequest request, CancellationToken cancellationToken = default);
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}
