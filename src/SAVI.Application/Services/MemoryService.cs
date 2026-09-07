using SAVI.Application.DTOs;
using SAVI.Application.Interfaces;
using SAVI.Core.Entities;
using SAVI.Core.Enums;

namespace SAVI.Application.Services;

public class MemoryService : IMemoryService
{
    private readonly IMemoryRepository _repository;

    public MemoryService(IMemoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<MemoryItemDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        return items.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<MemoryItemDto>> GetByCategoryAsync(MemoryType type, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetByTypeAsync(type, cancellationToken);
        return items.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<MemoryItemDto>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var items = await _repository.SearchAsync(query, cancellationToken);
        return items.Select(MapToDto).ToList();
    }

    public async Task<MemoryItemDto> AddAsync(CreateMemoryDto dto, string? sourceConversationId = null, CancellationToken cancellationToken = default)
    {
        var item = new MemoryItem
        {
            Id = Guid.NewGuid().ToString(),
            Type = dto.Type,
            Content = dto.Content,
            Importance = dto.Importance,
            Confidence = 1.0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = dto.ExpiresAt,
            SourceConversationId = sourceConversationId
        };

        await _repository.AddAsync(item, cancellationToken);
        return MapToDto(item);
    }

    public async Task<MemoryItemDto?> UpdateAsync(string id, UpdateMemoryDto dto, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(id, cancellationToken);
        if (item == null) return null;

        item.Type = dto.Type;
        item.Content = dto.Content;
        item.Importance = dto.Importance;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(item, cancellationToken);
        return MapToDto(item);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(id, cancellationToken);
    }

    public async Task ClearCategoryAsync(MemoryType type, CancellationToken cancellationToken = default)
    {
        await _repository.ClearCategoryAsync(type, cancellationToken);
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        await _repository.ClearAllAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryItem>> GetRelevantMemoriesAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        if (all.Count == 0) return Array.Empty<MemoryItem>();

        var active = all.Where(m => !m.ExpiresAt.HasValue || m.ExpiresAt > DateTimeOffset.UtcNow).ToList();

        var tokens = prompt.Split(new[] { ' ', ',', '.', '?', '!', ':', ';', '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 3)
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .ToList();

        if (tokens.Count == 0)
        {
            return active.Where(m => m.Type == MemoryType.Preference || m.Type == MemoryType.Instruction)
                         .OrderByDescending(m => m.Importance)
                         .Take(5)
                         .ToList();
        }

        var scored = active.Select(m =>
        {
            var contentLower = m.Content.ToLowerInvariant();
            var matches = tokens.Count(t => contentLower.Contains(t));
            var score = matches * 2.0 + m.Importance;
            if (m.Type == MemoryType.Preference || m.Type == MemoryType.Instruction)
            {
                score += 1.5;
            }
            return new { Item = m, Score = score };
        })
        .Where(x => x.Score > 1.0)
        .OrderByDescending(x => x.Score)
        .Take(5)
        .Select(x => x.Item)
        .ToList();

        return scored;
    }

    private static MemoryItemDto MapToDto(MemoryItem item)
    {
        return new MemoryItemDto
        {
            Id = item.Id,
            Type = item.Type,
            Content = item.Content,
            Importance = item.Importance,
            Confidence = item.Confidence,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            ExpiresAt = item.ExpiresAt,
            SourceConversationId = item.SourceConversationId
        };
    }
}
