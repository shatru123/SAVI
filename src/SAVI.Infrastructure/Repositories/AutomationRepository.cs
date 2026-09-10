using Microsoft.EntityFrameworkCore;
using SAVI.Application.Interfaces;
using SAVI.Core.Entities;
using SAVI.Infrastructure.Persistence;

namespace SAVI.Infrastructure.Repositories;

public class AutomationRepository : IAutomationRepository
{
    private readonly SaviDbContext _db;

    public AutomationRepository(SaviDbContext db)
    {
        _db = db;
    }

    public async Task<AutomationItem?> GetByIdAsync(string id, string? userId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.AutomationItems.AsQueryable();
        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(a => a.UserId == userId);
        }
        return await query.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<AutomationItem>> GetAllAsync(string? userId = null, bool? isEnabled = null, CancellationToken cancellationToken = default)
    {
        var query = _db.AutomationItems.AsQueryable();
        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(a => a.UserId == userId);
        }
        if (isEnabled.HasValue)
        {
            query = query.Where(a => a.IsEnabled == isEnabled.Value);
        }

        var list = await query.ToListAsync(cancellationToken);
        return list.OrderByDescending(a => a.UpdatedAt).ToList();
    }

    public async Task<IReadOnlyList<AutomationItem>> GetDueAutomationsAsync(DateTimeOffset referenceTime, CancellationToken cancellationToken = default)
    {
        var list = await _db.AutomationItems
            .Where(a => a.IsEnabled)
            .ToListAsync(cancellationToken);

        // Filter in memory for DateTimeOffset comparison with converter safety
        return list
            .Where(a => a.NextExecutionAt == null || a.NextExecutionAt <= referenceTime)
            .ToList();
    }

    public async Task<AutomationItem> AddAsync(AutomationItem item, CancellationToken cancellationToken = default)
    {
        await _db.AutomationItems.AddAsync(item, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task UpdateAsync(AutomationItem item, CancellationToken cancellationToken = default)
    {
        item.UpdatedAt = DateTimeOffset.UtcNow;
        _db.AutomationItems.Update(item);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, string? userId = null, CancellationToken cancellationToken = default)
    {
        var item = await GetByIdAsync(id, userId, cancellationToken);
        if (item != null)
        {
            // Also clean up associated logs
            var logs = await _db.AutomationExecutionLogs
                .Where(l => l.AutomationId == id)
                .ToListAsync(cancellationToken);
            if (logs.Count > 0)
            {
                _db.AutomationExecutionLogs.RemoveRange(logs);
            }

            _db.AutomationItems.Remove(item);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task AddLogAsync(AutomationExecutionLog log, CancellationToken cancellationToken = default)
    {
        await _db.AutomationExecutionLogs.AddAsync(log, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AutomationExecutionLog>> GetLogsAsync(string automationId, int limit = 50, CancellationToken cancellationToken = default)
    {
        var list = await _db.AutomationExecutionLogs
            .Where(l => l.AutomationId == automationId)
            .ToListAsync(cancellationToken);

        return list
            .OrderByDescending(l => l.ExecutedAt)
            .Take(limit)
            .ToList();
    }
}
