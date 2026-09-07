using SAVI.Core.Entities;
using SAVI.Core.Enums;

namespace SAVI.Core.Interfaces;

public interface ITaskStateMachine
{
    Task<TaskItem> CreateTaskAsync(string title, string description, IReadOnlyList<string> stepNames, CancellationToken cancellationToken = default);
    Task<TaskItem> AdvanceStepAsync(string taskId, string stepId, TaskState newState, string? result = null, CancellationToken cancellationToken = default);
    Task<TaskItem> SetTaskStateAsync(string taskId, TaskState newState, string? summary = null, CancellationToken cancellationToken = default);
    Task<TaskItem?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default);
}
