using System.Text.Json;
using SAVI.Application.DTOs;
using SAVI.Application.Interfaces;
using SAVI.Core.Entities;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;

namespace SAVI.Application.Services;

public class TaskService : ITaskService, ITaskStateMachine
{
    private readonly ITaskRepository _repository;

    public TaskService(ITaskRepository repository)
    {
        _repository = repository;
    }

    public async Task<TaskItemDto> CreateTaskAsync(string title, string description, IReadOnlyList<string> steps, CancellationToken cancellationToken = default)
    {
        var taskItem = await ((ITaskStateMachine)this).CreateTaskAsync(title, description, steps, cancellationToken);
        return MapToDto(taskItem);
    }

    async Task<TaskItem> ITaskStateMachine.CreateTaskAsync(string title, string description, IReadOnlyList<string> stepNames, CancellationToken cancellationToken)
    {
        var stepList = stepNames.Select((name, idx) => new TaskStep
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Order = idx,
            State = idx == 0 ? TaskState.Running : TaskState.Pending,
            RequiresApproval = name.Contains("delete", StringComparison.OrdinalIgnoreCase) ||
                               name.Contains("remove", StringComparison.OrdinalIgnoreCase) ||
                               name.Contains("drop", StringComparison.OrdinalIgnoreCase)
        }).ToList();

        var task = new TaskItem
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Description = description,
            State = TaskState.Running,
            ProgressPercentage = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            StepsJson = JsonSerializer.Serialize(stepList)
        };

        await _repository.AddAsync(task, cancellationToken);
        return task;
    }

    public async Task<TaskItemDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(id, cancellationToken);
        return item == null ? null : MapToDto(item);
    }

    public async Task<TaskItem?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        return await _repository.GetByIdAsync(taskId, cancellationToken);
    }

    public async Task<IReadOnlyList<TaskItemDto>> GetAllAsync(TaskState? state = null, CancellationToken cancellationToken = default)
    {
        var items = await _repository.GetAllAsync(state, cancellationToken);
        return items.Select(MapToDto).ToList();
    }

    public async Task<TaskItemDto> UpdateStepAsync(string taskId, string stepId, TaskState state, string? result = null, CancellationToken cancellationToken = default)
    {
        var updated = await AdvanceStepAsync(taskId, stepId, state, result, cancellationToken);
        return MapToDto(updated);
    }

    public async Task<TaskItem> AdvanceStepAsync(string taskId, string stepId, TaskState newState, string? result = null, CancellationToken cancellationToken = default)
    {
        var task = await _repository.GetByIdAsync(taskId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task {taskId} not found");

        var steps = string.IsNullOrWhiteSpace(task.StepsJson)
            ? new List<TaskStep>()
            : JsonSerializer.Deserialize<List<TaskStep>>(task.StepsJson) ?? new List<TaskStep>();

        var step = steps.FirstOrDefault(s => s.Id == stepId);
        if (step != null)
        {
            step.State = newState;
            if (result != null) step.Result = result;
        }

        var completedCount = steps.Count(s => s.State == TaskState.Completed);
        task.ProgressPercentage = steps.Count == 0 ? 100 : (int)((double)completedCount / steps.Count * 100);

        if (newState == TaskState.Completed)
        {
            var next = steps.OrderBy(s => s.Order).FirstOrDefault(s => s.State == TaskState.Pending);
            if (next != null)
            {
                next.State = next.RequiresApproval && !next.IsApproved ? TaskState.WaitingForApproval : TaskState.Running;
                if (next.State == TaskState.WaitingForApproval)
                {
                    task.State = TaskState.WaitingForApproval;
                }
            }
            else
            {
                task.State = TaskState.Completed;
            }
        }
        else if (newState == TaskState.Failed)
        {
            task.State = TaskState.Failed;
            task.ErrorMessage = result;
        }

        task.UpdatedAt = DateTimeOffset.UtcNow;
        task.StepsJson = JsonSerializer.Serialize(steps);

        await _repository.UpdateAsync(task, cancellationToken);
        return task;
    }

    public async Task<TaskItemDto> ApproveStepAsync(string taskId, string stepId, bool approved, CancellationToken cancellationToken = default)
    {
        var task = await _repository.GetByIdAsync(taskId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task {taskId} not found");

        var steps = string.IsNullOrWhiteSpace(task.StepsJson)
            ? new List<TaskStep>()
            : JsonSerializer.Deserialize<List<TaskStep>>(task.StepsJson) ?? new List<TaskStep>();

        var step = steps.FirstOrDefault(s => s.Id == stepId);
        if (step != null)
        {
            step.IsApproved = approved;
            step.State = approved ? TaskState.Running : TaskState.Cancelled;
            if (approved)
            {
                task.State = TaskState.Running;
            }
            else
            {
                task.State = TaskState.Cancelled;
            }
        }

        task.UpdatedAt = DateTimeOffset.UtcNow;
        task.StepsJson = JsonSerializer.Serialize(steps);

        await _repository.UpdateAsync(task, cancellationToken);
        return MapToDto(task);
    }

    public async Task<TaskItem> SetTaskStateAsync(string taskId, TaskState newState, string? summary = null, CancellationToken cancellationToken = default)
    {
        var task = await _repository.GetByIdAsync(taskId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task {taskId} not found");

        task.State = newState;
        if (summary != null) task.ResultSummary = summary;
        task.UpdatedAt = DateTimeOffset.UtcNow;

        await _repository.UpdateAsync(task, cancellationToken);
        return task;
    }

    public async Task CancelTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        await SetTaskStateAsync(taskId, TaskState.Cancelled, "Cancelled by user", cancellationToken);
    }

    private static TaskItemDto MapToDto(TaskItem item)
    {
        IReadOnlyList<TaskStepDto> steps = string.IsNullOrWhiteSpace(item.StepsJson)
            ? new List<TaskStepDto>()
            : (JsonSerializer.Deserialize<List<TaskStep>>(item.StepsJson) ?? new List<TaskStep>())
                .Select(s => new TaskStepDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    State = s.State,
                    Result = s.Result,
                    Order = s.Order,
                    RequiresApproval = s.RequiresApproval,
                    IsApproved = s.IsApproved
                }).ToList();

        return new TaskItemDto
        {
            Id = item.Id,
            Title = item.Title,
            Description = item.Description,
            State = item.State,
            ProgressPercentage = item.ProgressPercentage,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
            Steps = steps,
            ResultSummary = item.ResultSummary,
            ErrorMessage = item.ErrorMessage
        };
    }
}
