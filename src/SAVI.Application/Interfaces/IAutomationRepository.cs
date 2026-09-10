using SAVI.Core.Entities;

namespace SAVI.Application.Interfaces;

public interface IAutomationRepository
{
    Task<AutomationItem?> GetByIdAsync(string id, string? userId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutomationItem>> GetAllAsync(string? userId = null, bool? isEnabled = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutomationItem>> GetDueAutomationsAsync(DateTimeOffset referenceTime, CancellationToken cancellationToken = default);
    Task<AutomationItem> AddAsync(AutomationItem item, CancellationToken cancellationToken = default);
    Task UpdateAsync(AutomationItem item, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, string? userId = null, CancellationToken cancellationToken = default);

    Task AddLogAsync(AutomationExecutionLog log, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutomationExecutionLog>> GetLogsAsync(string automationId, int limit = 50, CancellationToken cancellationToken = default);
}
