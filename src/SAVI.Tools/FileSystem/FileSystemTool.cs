using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Tools.FileSystem;

public class FileSystemTool : ITool
{
    private readonly IPermissionGuard _permissionGuard;
    private readonly IAuditService _auditService;

    public FileSystemTool(IPermissionGuard permissionGuard, IAuditService auditService)
    {
        _permissionGuard = permissionGuard;
        _auditService = auditService;
    }

    public string Name => "file_system";
    public string Description => "Enables file inspection, reading, writing, and safe deletion within permitted directories.";
    public PermissionLevel PermissionLevel => PermissionLevel.Controlled;

    public async Task<ToolExecutionResult> ExecuteAsync(ToolInput input, CancellationToken cancellationToken = default)
    {
        var action = input.Action.ToLowerInvariant();
        var path = input.Arguments.GetValueOrDefault("path") ?? ".";

        if (action == "list" || action == "dir")
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                if (!Directory.Exists(fullPath))
                {
                    return ToolExecutionResult.Failed(Name, $"Directory does not exist: {path}");
                }

                var entries = Directory.GetFileSystemEntries(fullPath)
                    .Select(e =>
                    {
                        var isDir = Directory.Exists(e);
                        var info = new FileInfo(e);
                        return $"{(isDir ? "[DIR] " : "[FILE]")} {Path.GetFileName(e)} ({(isDir ? "-" : info.Length + " bytes")})";
                    }).Take(100).ToList();

                var output = entries.Count == 0
                    ? $"Directory {path} is empty."
                    : $"Files in {path}:\n" + string.Join("\n", entries);

                await _auditService.LogAsync($"List directory: {path}", Name, PermissionLevel.Safe, "Success", $"{entries.Count} items found", input.UserConfirmed, input.TaskId, cancellationToken);
                return ToolExecutionResult.Succeeded(Name, output);
            }
            catch (Exception ex)
            {
                return ToolExecutionResult.Failed(Name, $"Error listing directory: {ex.Message}");
            }
        }

        if (action == "read")
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                if (!File.Exists(fullPath))
                {
                    return ToolExecutionResult.Failed(Name, $"File does not exist: {path}");
                }

                var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
                if (content.Length > 20000)
                {
                    content = content[..19990] + "\n... [Content truncated]";
                }

                await _auditService.LogAsync($"Read file: {path}", Name, PermissionLevel.Safe, "Success", $"{content.Length} chars read", input.UserConfirmed, input.TaskId, cancellationToken);
                return ToolExecutionResult.Succeeded(Name, content);
            }
            catch (Exception ex)
            {
                return ToolExecutionResult.Failed(Name, $"Error reading file: {ex.Message}");
            }
        }

        if (action == "write" || action == "create")
        {
            var content = input.Arguments.GetValueOrDefault("content") ?? "";
            var controlledInput = input with { RequiredPermission = PermissionLevel.Controlled };

            if (!await _permissionGuard.CanExecuteAsync(controlledInput, cancellationToken))
            {
                return ToolExecutionResult.AwaitingApproval(Name, $"Permission required to write file: {path}");
            }

            try
            {
                var fullPath = Path.GetFullPath(path);
                var dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                await File.WriteAllTextAsync(fullPath, content, cancellationToken);
                await _auditService.LogAsync($"Wrote file: {path}", Name, PermissionLevel.Controlled, "Success", $"{content.Length} chars written", input.UserConfirmed, input.TaskId, cancellationToken);
                return ToolExecutionResult.Succeeded(Name, $"Successfully wrote file {path} ({content.Length} chars).", new[] { fullPath });
            }
            catch (Exception ex)
            {
                return ToolExecutionResult.Failed(Name, $"Error writing file: {ex.Message}");
            }
        }

        if (action == "delete")
        {
            var dangerousInput = input with { RequiredPermission = PermissionLevel.Dangerous };

            if (!await _permissionGuard.CanExecuteAsync(dangerousInput, cancellationToken))
            {
                return ToolExecutionResult.AwaitingApproval(Name, $"Deleting {path} is a destructive action. Explicit approval required.");
            }

            try
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    await _auditService.LogAsync($"Deleted file: {path}", Name, PermissionLevel.Dangerous, "Success", "File deleted", userApproved: true, input.TaskId, cancellationToken);
                    return ToolExecutionResult.Succeeded(Name, $"Permanently deleted file: {path}");
                }
                else if (Directory.Exists(fullPath))
                {
                    Directory.Delete(fullPath, recursive: true);
                    await _auditService.LogAsync($"Deleted directory: {path}", Name, PermissionLevel.Dangerous, "Success", "Directory deleted recursively", userApproved: true, input.TaskId, cancellationToken);
                    return ToolExecutionResult.Succeeded(Name, $"Permanently deleted directory: {path}");
                }
                else
                {
                    return ToolExecutionResult.Failed(Name, $"Target path does not exist: {path}");
                }
            }
            catch (Exception ex)
            {
                return ToolExecutionResult.Failed(Name, $"Error deleting target: {ex.Message}");
            }
        }

        return ToolExecutionResult.Failed(Name, $"Unsupported action '{action}'. Supported: list, read, write, delete.");
    }
}
