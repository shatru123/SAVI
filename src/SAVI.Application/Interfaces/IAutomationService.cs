using SAVI.Application.DTOs;

namespace SAVI.Application.Interfaces;

public interface IAutomationService
{
    Task<IReadOnlyList<AutomationDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<AutomationDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<AutomationDto> CreateAsync(CreateAutomationDto dto, CancellationToken cancellationToken = default);
    Task<AutomationDto?> UpdateAsync(string id, UpdateAutomationDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<bool> ToggleEnabledAsync(string id, CancellationToken cancellationToken = default);
    Task<AutomationExecutionLogDto> TriggerNowAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutomationExecutionLogDto>> GetLogsAsync(string automationId, int limit = 50, CancellationToken cancellationToken = default);
}
