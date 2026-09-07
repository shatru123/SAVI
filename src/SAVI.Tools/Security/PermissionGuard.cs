using SAVI.Application.Interfaces;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;

namespace SAVI.Tools.Security;

public class PermissionGuard : IPermissionGuard
{
    private readonly ISettingsService _settingsService;
    private readonly IAuditService _auditService;

    public PermissionGuard(ISettingsService settingsService, IAuditService auditService)
    {
        _settingsService = settingsService;
        _auditService = auditService;
    }

    public async Task<bool> CanExecuteAsync(ToolInput input, CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.GetSettingsAsync(cancellationToken);

        switch (input.RequiredPermission)
        {
            case PermissionLevel.Safe:
                return true;

            case PermissionLevel.Controlled:
                // If auto-approve is disabled, require explicit confirmation
                if (!settings.AutoApproveSafeTools && !input.UserConfirmed)
                {
                    await _auditService.LogAsync(
                        action: $"Requested approval for controlled tool: {input.ToolName} ({input.Action})",
                        toolName: input.ToolName,
                        level: PermissionLevel.Controlled,
                        status: "AwaitingConfirmation",
                        details: $"Action requires controlled permission.",
                        userApproved: false,
                        taskId: input.TaskId,
                        cancellationToken: cancellationToken);
                    return false;
                }
                return true;

            case PermissionLevel.Dangerous:
                if (!input.UserConfirmed)
                {
                    await _auditService.LogAsync(
                        action: $"BLOCKED execution of dangerous tool: {input.ToolName} ({input.Action})",
                        toolName: input.ToolName,
                        level: PermissionLevel.Dangerous,
                        status: "BlockedAwaitingUserApproval",
                        details: $"Dangerous operation requires explicit user confirmation.",
                        userApproved: false,
                        taskId: input.TaskId,
                        cancellationToken: cancellationToken);
                    return false;
                }
                return true;

            default:
                return false;
        }
    }

    public ActionApprovalRequest CreateApprovalRequest(ToolInput input)
    {
        var description = input.Action.ToLowerInvariant() switch
        {
            "delete" => $"Permanently delete file: {input.Arguments.GetValueOrDefault("path")}",
            "deletedirectory" => $"Permanently remove directory and its contents: {input.Arguments.GetValueOrDefault("path")}",
            "execute" => $"Execute system terminal command: '{input.Arguments.GetValueOrDefault("command")}'",
            _ => $"Execute {input.RequiredPermission.ToString().ToLowerInvariant()} tool action '{input.ToolName}.{input.Action}'"
        };

        return new ActionApprovalRequest
        {
            ActionId = Guid.NewGuid().ToString(),
            ToolName = input.ToolName,
            Description = description,
            PermissionLevel = input.RequiredPermission,
            Arguments = new Dictionary<string, string>(input.Arguments),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
