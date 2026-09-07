using SAVI.Core.Entities;

namespace SAVI.Application.Interfaces;

public interface IAuditRepository
{
    Task AddAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogEntry>> GetRecentAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task ClearAllAsync(CancellationToken cancellationToken = default);
}
