using Microsoft.EntityFrameworkCore;
using SAVI.Application.Interfaces;
using SAVI.Core.Entities;
using SAVI.Core.Enums;
using SAVI.Infrastructure.Persistence;

namespace SAVI.Infrastructure.Repositories;

public class MemoryRepository : IMemoryRepository
{
    private readonly SaviDbContext _db;

    public MemoryRepository(SaviDbContext db)
    {
        _db = db;
    }

    public async Task<MemoryItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _db.MemoryItems.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<MemoryItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = await _db.MemoryItems.ToListAsync(cancellationToken);
        return list.OrderByDescending(m => m.Importance)
                   .ThenByDescending(m => m.UpdatedAt)
                   .ToList();
    }

    public async Task<IReadOnlyList<MemoryItem>> GetByTypeAsync(MemoryType type, CancellationToken cancellationToken = default)
    {
        var list = await _db.MemoryItems.Where(m => m.Type == type).ToListAsync(cancellationToken);
        return list.OrderByDescending(m => m.Importance)
                   .ThenByDescending(m => m.UpdatedAt)
                   .ToList();
    }

    public async Task<IReadOnlyList<MemoryItem>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var qLower = query.ToLower();
        var list = await _db.MemoryItems
            .Where(m => m.Content.ToLower().Contains(qLower))
            .ToListAsync(cancellationToken);

        return list.OrderByDescending(m => m.Importance).ToList();
    }

    public async Task AddAsync(MemoryItem item, CancellationToken cancellationToken = default)
    {
        await _db.MemoryItems.AddAsync(item, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(MemoryItem item, CancellationToken cancellationToken = default)
    {
        _db.MemoryItems.Update(item);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var item = await _db.MemoryItems.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (item != null)
        {
            _db.MemoryItems.Remove(item);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ClearCategoryAsync(MemoryType type, CancellationToken cancellationToken = default)
    {
        var items = await _db.MemoryItems.Where(m => m.Type == type).ToListAsync(cancellationToken);
        if (items.Count > 0)
        {
            _db.MemoryItems.RemoveRange(items);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _db.MemoryItems.ToListAsync(cancellationToken);
        if (items.Count > 0)
        {
            _db.MemoryItems.RemoveRange(items);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        return await _db.MemoryItems.CountAsync(cancellationToken);
    }
}
