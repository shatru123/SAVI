using SAVI.Core.Enums;

namespace SAVI.Core.Models;

public sealed record ToolInput
{
    public string ToolName { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public Dictionary<string, string> Arguments { get; init; } = new();
    public PermissionLevel RequiredPermission { get; init; } = PermissionLevel.Safe;
    public bool UserConfirmed { get; init; }
    public string? TaskId { get; init; }
}
