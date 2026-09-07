using SAVI.Core.Enums;

namespace SAVI.Core.Entities;

public sealed class TaskStep
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TaskItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TaskState State { get; set; } = TaskState.Pending;
    public string? Result { get; set; }
    public int Order { get; set; }
    public bool RequiresApproval { get; set; }
    public bool IsApproved { get; set; }
}
