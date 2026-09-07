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
    Task<IReadOnlyList<ProviderMetadata>> GetMetadataAsync(CancellationToken cancellationToken = default);
}
