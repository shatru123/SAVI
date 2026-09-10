using SAVI.Core.Enums;

namespace SAVI.Core.Entities;

public sealed class TaskItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaskState State { get; set; } = TaskState.Pending;
    public int ProgressPercentage { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? StepsJson { get; set; }
    public string? ResultSummary { get; set; }
    public string? ErrorMessage { get; set; }
    public string? UserId { get; set; }
}
