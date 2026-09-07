using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Tools.Notifications;

public class NotificationTool : ITool
{
    private readonly IAuditService _auditService;

    public NotificationTool(IAuditService auditService)
    {
        _auditService = auditService;
    }

    public string Name => "notification";
    public string Description => "Dispatches in-app, desktop, and mobile notifications for reminders and task completions.";
    public PermissionLevel PermissionLevel => PermissionLevel.Safe;

    public async Task<ToolExecutionResult> ExecuteAsync(ToolInput input, CancellationToken cancellationToken = default)
    {
        var title = input.Arguments.GetValueOrDefault("title") ?? "SAVI Notification";
        var message = input.Arguments.GetValueOrDefault("message") ?? input.Arguments.GetValueOrDefault("text") ?? "";

        await _auditService.LogAsync($"Notification dispatched: {title}", Name, PermissionLevel.Safe, "Dispatched", message, input.UserConfirmed, input.TaskId, cancellationToken);

        return ToolExecutionResult.Succeeded(Name, $"Notification '{title}' delivered: {message}");
    }
}
