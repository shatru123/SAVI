using SAVI.Core.Enums;

namespace SAVI.Application.DTOs;

public sealed record TaskStepDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public TaskState State { get; init; }
    public string? Result { get; init; }
    public int Order { get; init; }
    public bool RequiresApproval { get; init; }
    public bool IsApproved { get; init; }
}

public sealed record TaskItemDto
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public TaskState State { get; init; }
    public int ProgressPercentage { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public IReadOnlyList<TaskStepDto> Steps { get; init; } = Array.Empty<TaskStepDto>();
    public string? ResultSummary { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed record ApproveTaskStepDto
{
    public string TaskId { get; init; } = string.Empty;
    public string StepId { get; init; } = string.Empty;
    public bool Approved { get; init; }
}
