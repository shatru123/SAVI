using Microsoft.EntityFrameworkCore;
using SAVI.Application.Interfaces;
using SAVI.Core.Entities;
using SAVI.Infrastructure.Persistence;

namespace SAVI.Infrastructure.Repositories;

public class AuditRepository : IAuditRepository
{
    private readonly SaviDbContext _db;

    public AuditRepository(SaviDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        await _db.AuditLogs.AddAsync(entry, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        var list = await _db.AuditLogs.ToListAsync(cancellationToken);
        return list.OrderByDescending(a => a.Timestamp).Take(limit).ToList();
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        var all = await _db.AuditLogs.ToListAsync(cancellationToken);
        if (all.Count > 0)
        {
            _db.AuditLogs.RemoveRange(all);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
