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

    public async Task<IReadOnlyList<MemoryItem>> GetAllAsync(string? userId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.MemoryItems.AsQueryable();
        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(m => m.UserId == userId);
        }
        var list = await query.ToListAsync(cancellationToken);
        return list.OrderByDescending(m => m.Importance)
                   .ThenByDescending(m => m.UpdatedAt)
                   .ToList();
    }

    public async Task<IReadOnlyList<MemoryItem>> GetByTypeAsync(MemoryType type, string? userId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.MemoryItems.Where(m => m.Type == type);
        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(m => m.UserId == userId);
        }
        var list = await query.ToListAsync(cancellationToken);
        return list.OrderByDescending(m => m.Importance)
                   .ThenByDescending(m => m.UpdatedAt)
                   .ToList();
    }

    public async Task<IReadOnlyList<MemoryItem>> SearchAsync(string query, string? userId = null, CancellationToken cancellationToken = default)
    {
        var qLower = query.ToLower();
        var q = _db.MemoryItems.Where(m => m.Content.ToLower().Contains(qLower));
        if (!string.IsNullOrEmpty(userId))
        {
            q = q.Where(m => m.UserId == userId);
        }
        var list = await q.ToListAsync(cancellationToken);
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

    public async Task ClearCategoryAsync(MemoryType type, string? userId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.MemoryItems.Where(m => m.Type == type);
        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(m => m.UserId == userId);
        }
        var items = await query.ToListAsync(cancellationToken);
        if (items.Count > 0)
        {
            _db.MemoryItems.RemoveRange(items);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ClearAllAsync(string? userId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.MemoryItems.AsQueryable();
        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(m => m.UserId == userId);
        }
        var items = await query.ToListAsync(cancellationToken);
        if (items.Count > 0)
        {
            _db.MemoryItems.RemoveRange(items);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> GetCountAsync(string? userId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.MemoryItems.AsQueryable();
        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(m => m.UserId == userId);
        }
        return await query.CountAsync(cancellationToken);
    }
}
