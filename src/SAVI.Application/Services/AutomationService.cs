using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SAVI.Application.DTOs;
using SAVI.Application.Interfaces;
using SAVI.Core.Entities;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;

namespace SAVI.Application.Services;

public class AutomationService : IAutomationService
{
    private readonly IAutomationRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IServiceScopeFactory _scopeFactory;

    public AutomationService(
        IAutomationRepository repository,
        ICurrentUserService currentUserService,
        IServiceScopeFactory scopeFactory)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _scopeFactory = scopeFactory;
    }

    private string? EffectiveUserId => _currentUserService.IsOwner ? null : _currentUserService.UserId;

    public async Task<IReadOnlyList<AutomationDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated)
        {
            return Array.Empty<AutomationDto>();
        }

        var items = await _repository.GetAllAsync(EffectiveUserId, null, cancellationToken);
        return items.Select(MapToDto).ToList();
    }

    public async Task<AutomationDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated)
        {
            return null;
        }

        var item = await _repository.GetByIdAsync(id, EffectiveUserId, cancellationToken);
        return item != null ? MapToDto(item) : null;
    }

    public async Task<AutomationDto> CreateAsync(CreateAutomationDto dto, CancellationToken cancellationToken = default)
    {
        var interval = dto.IntervalSeconds is > 0 ? dto.IntervalSeconds.Value : 3600;
        var item = new AutomationItem
        {
            UserId = _currentUserService.UserId,
            Name = dto.Name,
            Description = dto.Description,
            TriggerType = dto.TriggerType,
            IntervalSeconds = interval,
            CronExpression = dto.CronExpression,
            Prompt = dto.Prompt,
            IsEnabled = dto.IsEnabled,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            NextExecutionAt = dto.IsEnabled ? DateTimeOffset.UtcNow.AddSeconds(interval) : null
        };

        var created = await _repository.AddAsync(item, cancellationToken);
        return MapToDto(created);
    }

    public async Task<AutomationDto?> UpdateAsync(string id, UpdateAutomationDto dto, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(id, EffectiveUserId, cancellationToken);
        if (item == null) return null;

        var interval = dto.IntervalSeconds is > 0 ? dto.IntervalSeconds.Value : (item.IntervalSeconds ?? 3600);
        item.Name = dto.Name;
        item.Description = dto.Description;
        item.TriggerType = dto.TriggerType;
        item.IntervalSeconds = interval;
        item.CronExpression = dto.CronExpression;
        item.Prompt = dto.Prompt;
        item.IsEnabled = dto.IsEnabled;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        if (dto.IsEnabled && (item.NextExecutionAt == null || item.NextExecutionAt < DateTimeOffset.UtcNow))
        {
            item.NextExecutionAt = DateTimeOffset.UtcNow.AddSeconds(interval);
        }
        else if (!dto.IsEnabled)
        {
            item.NextExecutionAt = null;
        }

        await _repository.UpdateAsync(item, cancellationToken);
        return MapToDto(item);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(id, EffectiveUserId, cancellationToken);
        if (item == null) return false;

        await _repository.DeleteAsync(id, EffectiveUserId, cancellationToken);
        return true;
    }

    public async Task<bool> ToggleEnabledAsync(string id, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(id, EffectiveUserId, cancellationToken);
        if (item == null) return false;

        item.IsEnabled = !item.IsEnabled;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        if (item.IsEnabled)
        {
            var interval = item.IntervalSeconds ?? 3600;
            item.NextExecutionAt = DateTimeOffset.UtcNow.AddSeconds(interval);
        }
        else
        {
            item.NextExecutionAt = null;
        }

        await _repository.UpdateAsync(item, cancellationToken);
        return true;
    }

    public async Task<AutomationExecutionLogDto> TriggerNowAsync(string id, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(id, EffectiveUserId, cancellationToken);
        if (item == null)
        {
            throw new KeyNotFoundException($"Automation with id {id} not found.");
        }

        var sw = Stopwatch.StartNew();
        bool success = false;
        string? result = null;
        string? error = null;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetService<IAgentOrchestrator>();
            if (orchestrator != null)
            {
                var response = await orchestrator.ProcessAsync(new AgentRequest
                {
                    Message = item.Prompt,
                    ConversationId = Guid.NewGuid().ToString(),
                    VoiceActive = false
                }, cancellationToken);

                success = response.Success;
                result = response.Message;
                if (!response.Success)
                {
                    error = response.Message;
                }
            }
            else
            {
                success = true;
                result = $"Executed automation prompt: \"{item.Prompt}\" (Simulated/Registry mode)";
            }
        }
        catch (Exception ex)
        {
            success = false;
            error = ex.Message;
        }
        finally
        {
            sw.Stop();
        }

        var log = new AutomationExecutionLog
        {
            Id = Guid.NewGuid().ToString(),
            AutomationId = item.Id,
            ExecutedAt = DateTimeOffset.UtcNow,
            Success = success,
            Result = result,
            ErrorMessage = error,
            DurationMs = sw.ElapsedMilliseconds
        };

        await _repository.AddLogAsync(log, cancellationToken);

        item.LastExecutedAt = DateTimeOffset.UtcNow;
        if (item.IsEnabled)
        {
            var interval = item.IntervalSeconds ?? 3600;
            item.NextExecutionAt = DateTimeOffset.UtcNow.AddSeconds(interval);
        }
        await _repository.UpdateAsync(item, cancellationToken);

        return new AutomationExecutionLogDto(
            log.Id,
            log.AutomationId,
            log.ExecutedAt,
            log.Success,
            log.Result,
            log.ErrorMessage,
            log.DurationMs
        );
    }

    public async Task<IReadOnlyList<AutomationExecutionLogDto>> GetLogsAsync(string automationId, int limit = 50, CancellationToken cancellationToken = default)
    {
        var item = await _repository.GetByIdAsync(automationId, EffectiveUserId, cancellationToken);
        if (item == null)
        {
            return Array.Empty<AutomationExecutionLogDto>();
        }

        var logs = await _repository.GetLogsAsync(automationId, limit, cancellationToken);
        return logs.Select(l => new AutomationExecutionLogDto(
            l.Id,
            l.AutomationId,
            l.ExecutedAt,
            l.Success,
            l.Result,
            l.ErrorMessage,
            l.DurationMs
        )).ToList();
    }

    private static AutomationDto MapToDto(AutomationItem a) => new(
        a.Id,
        a.UserId,
        a.Name,
        a.Description,
        a.TriggerType,
        a.IntervalSeconds,
        a.CronExpression,
        a.Prompt,
        a.IsEnabled,
        a.LastExecutedAt,
        a.NextExecutionAt,
        a.CreatedAt,
        a.UpdatedAt
    );
}
