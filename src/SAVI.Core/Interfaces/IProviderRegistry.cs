using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Core.Interfaces;

public interface IProviderRegistry
{
    void Register(ICapabilityProvider provider);
    IReadOnlyCollection<ICapabilityProvider> GetAll();
    IReadOnlyCollection<ICapabilityProvider> GetByCapability(string capability);
    ICapabilityProvider? GetById(string id);
    IReadOnlyList<ICapabilityProvider> RankProviders(TaskRequest request);
    void RecordResult(string providerId, long latencyMs, bool succeeded, bool rateLimited = false, TimeSpan? retryAfter = null);
    Task<IReadOnlyList<ProviderMetadata>> GetMetadataAsync(CancellationToken cancellationToken = default);
}
