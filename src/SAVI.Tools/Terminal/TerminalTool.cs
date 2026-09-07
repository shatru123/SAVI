using System.Diagnostics;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Tools.Terminal;

public class TerminalTool : ITool
{
    private readonly IPermissionGuard _permissionGuard;
    private readonly IAuditService _auditService;

    private static readonly HashSet<string> BlockedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "rm -rf /", "rm -rf *", ":(){ :|:& };:", "mkfs", "dd if=/dev/zero", "fdisk", "shutdown", "reboot"
    };

    public TerminalTool(IPermissionGuard permissionGuard, IAuditService auditService)
    {
        _permissionGuard = permissionGuard;
        _auditService = auditService;
    }

    public string Name => "terminal";
    public string Description => "Executes shell and terminal commands with strict safety guardrails.";
    public PermissionLevel PermissionLevel => PermissionLevel.Dangerous;

    public async Task<ToolExecutionResult> ExecuteAsync(ToolInput input, CancellationToken cancellationToken = default)
    {
        var command = input.Arguments.GetValueOrDefault("command") ?? input.Arguments.GetValueOrDefault("cmd") ?? "";
        if (string.IsNullOrWhiteSpace(command))
        {
            return ToolExecutionResult.Failed(Name, "No command provided for execution.");
        }

        // Check destructive blacklist
        if (BlockedCommands.Any(b => command.Contains(b, StringComparison.OrdinalIgnoreCase)))
        {
            await _auditService.LogAsync($"BLOCKED dangerous command: {command}", Name, PermissionLevel.Dangerous, "BlockedBlacklist", "Command matched catastrophic destruction rule", userApproved: false, input.TaskId, cancellationToken);
            return ToolExecutionResult.Failed(Name, "This command is classified as high-risk catastrophic destruction and is blocked by system safety guardrails.");
        }

        var dangerousInput = input with { RequiredPermission = PermissionLevel.Dangerous };
        if (!await _permissionGuard.CanExecuteAsync(dangerousInput, cancellationToken))
        {
            return ToolExecutionResult.AwaitingApproval(Name, $"Execution of command '{command}' requires user approval.");
        }

        try
        {
            var isWindows = OperatingSystem.IsWindows();
            var fileName = isWindows ? "cmd.exe" : "/bin/bash";
            var args = isWindows ? $"/c \"{command}\"" : $"-c \"{command.Replace("\"", "\\\"")}\"";

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Environment.CurrentDirectory
                }
            };

            process.Start();

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var stdout = await process.StandardOutput.ReadToEndAsync(linked.Token);
            var stderr = await process.StandardError.ReadToEndAsync(linked.Token);

            await process.WaitForExitAsync(linked.Token);

            var fullOutput = string.IsNullOrWhiteSpace(stderr)
                ? stdout
                : $"{stdout}\n[Error Stream]:\n{stderr}";

            if (fullOutput.Length > 10000)
            {
                fullOutput = fullOutput[..9950] + "\n... [Output truncated]";
            }

            var success = process.ExitCode == 0;
            await _auditService.LogAsync(
                action: $"Executed command: {command}",
                toolName: Name,
                level: PermissionLevel.Dangerous,
                status: success ? "Success" : $"ExitCode:{process.ExitCode}",
                details: $"Exit Code: {process.ExitCode}",
                userApproved: true,
                taskId: input.TaskId,
                cancellationToken: cancellationToken);

            return success
                ? ToolExecutionResult.Succeeded(Name, fullOutput)
                : ToolExecutionResult.Failed(Name, $"Command exited with code {process.ExitCode}.\n{fullOutput}");
        }
        catch (Exception ex)
        {
            return ToolExecutionResult.Failed(Name, $"Execution error: {ex.Message}");
        }
    }
}
