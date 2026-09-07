using SAVI.Core.Enums;

namespace SAVI.Core.ValueObjects;

public sealed record ToolExecutionResult
{
    public bool Success { get; init; }
    public string ToolName { get; init; } = string.Empty;
    public string? Output { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<string> Artifacts { get; init; } = Array.Empty<string>();
    public DateTimeOffset ExecutedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool RequiredApproval { get; init; }
    public bool ApprovalGranted { get; init; }

    public static ToolExecutionResult Succeeded(string toolName, string output, IReadOnlyList<string>? artifacts = null) =>
        new()
        {
            Success = true,
            ToolName = toolName,
            Output = output,
            Artifacts = artifacts ?? Array.Empty<string>(),
            ExecutedAt = DateTimeOffset.UtcNow
        };

    public static ToolExecutionResult Failed(string toolName, string error) =>
        new()
        {
            Success = false,
            ToolName = toolName,
            ErrorMessage = error,
            ExecutedAt = DateTimeOffset.UtcNow
        };

    public static ToolExecutionResult AwaitingApproval(string toolName, string prompt) =>
        new()
        {
            Success = false,
            ToolName = toolName,
            RequiredApproval = true,
            Output = prompt,
            ExecutedAt = DateTimeOffset.UtcNow
        };
}
