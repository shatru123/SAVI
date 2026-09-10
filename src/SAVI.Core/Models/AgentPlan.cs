using SAVI.Core.Enums;

namespace SAVI.Core.Models;

public enum PlanExecutionStatus
{
    Pending = 0,
    Planning = 1,
    Executing = 2,
    WaitingForApproval = 3,
    Completed = 4,
    Failed = 5,
    Cancelled = 6
}

public sealed record PlanStep
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public int Order { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string SkillOrToolName { get; init; } = string.Empty;
    public string Capability { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> Arguments { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<string> Dependencies { get; init; } = Array.Empty<string>();
    public TaskState State { get; set; } = TaskState.Pending;
    public bool RequiresConfirmation { get; init; }
    public string? Result { get; set; }
}

public sealed record AgentPlan
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Goal { get; init; } = string.Empty;
    public string RequestType { get; init; } = "Task";
    public IReadOnlyList<PlanStep> Steps { get; init; } = Array.Empty<PlanStep>();
    public PlanExecutionStatus Status { get; set; } = PlanExecutionStatus.Pending;
    public VerificationPolicy VerificationPolicy { get; init; } = VerificationPolicy.Balanced;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
