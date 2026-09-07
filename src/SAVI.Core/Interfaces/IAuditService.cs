using SAVI.Core.Entities;
using SAVI.Core.Enums;

namespace SAVI.Core.Interfaces;

public interface IAuditService
{
    Task LogAsync(string action, string toolName, PermissionLevel level, string status, string details, bool userApproved, string? taskId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogEntry>> GetRecentLogsAsync(int limit = 100, CancellationToken cancellationToken = default);
}
