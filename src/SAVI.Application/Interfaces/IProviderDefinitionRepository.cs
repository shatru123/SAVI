using SAVI.Core.Entities;

namespace SAVI.Application.Interfaces;

public interface IProviderDefinitionRepository
{
    Task<IReadOnlyList<ApiProviderDefinition>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ApiProviderDefinition?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task AddAsync(ApiProviderDefinition definition, CancellationToken cancellationToken = default);
    Task UpdateAsync(ApiProviderDefinition definition, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
