using SAVI.Core.Enums;

namespace SAVI.Core.Models;

public sealed record ActionApprovalRequest
{
    public string ActionId { get; init; } = Guid.NewGuid().ToString();
    public string ToolName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public PermissionLevel PermissionLevel { get; init; } = PermissionLevel.Dangerous;
    public Dictionary<string, string> Arguments { get; init; } = new();
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
