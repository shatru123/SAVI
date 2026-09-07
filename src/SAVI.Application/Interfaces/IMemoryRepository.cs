using SAVI.Core.Entities;
using SAVI.Core.Enums;

namespace SAVI.Application.Interfaces;

public interface IMemoryRepository
{
    Task<MemoryItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryItem>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryItem>> GetByTypeAsync(MemoryType type, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryItem>> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task AddAsync(MemoryItem item, CancellationToken cancellationToken = default);
    Task UpdateAsync(MemoryItem item, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task ClearCategoryAsync(MemoryType type, CancellationToken cancellationToken = default);
    Task ClearAllAsync(CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
}
