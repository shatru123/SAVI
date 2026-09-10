using SAVI.Core.Entities;
using SAVI.Core.Enums;

namespace SAVI.Application.Interfaces;

public interface IMemoryRepository
{
    Task<MemoryItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryItem>> GetAllAsync(string? userId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryItem>> GetByTypeAsync(MemoryType type, string? userId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryItem>> SearchAsync(string query, string? userId = null, CancellationToken cancellationToken = default);
    Task AddAsync(MemoryItem item, CancellationToken cancellationToken = default);
    Task UpdateAsync(MemoryItem item, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task ClearCategoryAsync(MemoryType type, string? userId = null, CancellationToken cancellationToken = default);
    Task ClearAllAsync(string? userId = null, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(string? userId = null, CancellationToken cancellationToken = default);
}
