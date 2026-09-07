using SAVI.Core.Entities;
using SAVI.Core.Enums;

namespace SAVI.Application.Interfaces;

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskItem>> GetAllAsync(TaskState? state = null, CancellationToken cancellationToken = default);
    Task AddAsync(TaskItem item, CancellationToken cancellationToken = default);
    Task UpdateAsync(TaskItem item, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
