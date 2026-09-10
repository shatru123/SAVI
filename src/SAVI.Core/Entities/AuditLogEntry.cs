using SAVI.Core.Enums;

namespace SAVI.Core.Entities;

public sealed class AuditLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string Action { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public PermissionLevel PermissionLevel { get; set; } = PermissionLevel.Safe;
    public string Status { get; set; } = "Executed";
    public string Details { get; set; } = string.Empty;
    public bool UserApproved { get; set; }
    public string? TaskId { get; set; }
    public string? UserId { get; set; }
    public string? TargetUserId { get; set; }
    public string? TargetResourceId { get; set; }
}
