using SAVI.Application.Interfaces;
using SAVI.Core.Entities;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Infrastructure.Security;

namespace SAVI.Infrastructure.Audit;

public class AuditService : IAuditService
{
    private readonly IAuditRepository _repository;

    public AuditService(IAuditRepository repository)
    {
        _repository = repository;
    }

    public async Task LogAsync(
        string action,
        string toolName,
        PermissionLevel level,
        string status,
        string details,
        bool userApproved,
        string? taskId = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new AuditLogEntry
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow,
            Action = action,
            ToolName = toolName,
            PermissionLevel = level,
            Status = status,
            Details = SecretMasker.Mask(details),
            UserApproved = userApproved,
            TaskId = taskId
        };

        await _repository.AddAsync(entry, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetRecentLogsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        return await _repository.GetRecentAsync(limit, cancellationToken);
    }
}
