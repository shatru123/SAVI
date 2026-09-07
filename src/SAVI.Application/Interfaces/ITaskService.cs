using SAVI.Application.DTOs;
using SAVI.Core.Enums;

namespace SAVI.Application.Interfaces;

public interface ITaskService
{
    Task<TaskItemDto> CreateTaskAsync(string title, string description, IReadOnlyList<string> steps, CancellationToken cancellationToken = default);
    Task<TaskItemDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskItemDto>> GetAllAsync(TaskState? state = null, CancellationToken cancellationToken = default);
    Task<TaskItemDto> UpdateStepAsync(string taskId, string stepId, TaskState state, string? result = null, CancellationToken cancellationToken = default);
    Task<TaskItemDto> ApproveStepAsync(string taskId, string stepId, bool approved, CancellationToken cancellationToken = default);
    Task CancelTaskAsync(string taskId, CancellationToken cancellationToken = default);
}
