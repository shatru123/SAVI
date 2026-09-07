using SAVI.Application.DTOs;
using SAVI.Core.Entities;
using SAVI.Core.Enums;

namespace SAVI.Application.Interfaces;

public interface IMemoryService
{
    Task<IReadOnlyList<MemoryItemDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryItemDto>> GetByCategoryAsync(MemoryType type, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryItemDto>> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task<MemoryItemDto> AddAsync(CreateMemoryDto dto, string? sourceConversationId = null, CancellationToken cancellationToken = default);
    Task<MemoryItemDto?> UpdateAsync(string id, UpdateMemoryDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task ClearCategoryAsync(MemoryType type, CancellationToken cancellationToken = default);
    Task ClearAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemoryItem>> GetRelevantMemoriesAsync(string prompt, CancellationToken cancellationToken = default);
}
