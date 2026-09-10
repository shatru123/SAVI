using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SAVI.Application.Interfaces;
using SAVI.Core.Entities;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;

namespace SAVI.Infrastructure.Automation;

public class AutomationScheduler : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutomationScheduler> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);

    public AutomationScheduler(
        IServiceScopeFactory scopeFactory,
        ILogger<AutomationScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SAVI Automation Scheduler background service started.");

        // Initial delay to allow DB schema initialization to complete
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueAutomationsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in SAVI Automation Scheduler cycle.");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("SAVI Automation Scheduler background service stopped.");
    }

    private async Task ProcessDueAutomationsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAutomationRepository>();
        var orchestrator = scope.ServiceProvider.GetService<IAgentOrchestrator>();

        var dueItems = await repo.GetDueAutomationsAsync(DateTimeOffset.UtcNow, stoppingToken);
        if (dueItems.Count == 0) return;

        _logger.LogInformation("Processing {Count} due automation item(s).", dueItems.Count);

        foreach (var item in dueItems)
        {
            if (stoppingToken.IsCancellationRequested) break;

            _logger.LogInformation("Executing automation: {Name} ({Id})", item.Name, item.Id);

            var sw = Stopwatch.StartNew();
            bool success = false;
            string? result = null;
            string? error = null;

            try
            {
                if (orchestrator != null)
                {
                    var response = await orchestrator.ProcessAsync(new AgentRequest
                    {
                        Message = item.Prompt,
                        ConversationId = Guid.NewGuid().ToString(),
                        VoiceActive = false
                    }, stoppingToken);

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
                _logger.LogError(ex, "Failed executing automation {Id}", item.Id);
            }
            finally
            {
                sw.Stop();
            }

            // Log execution
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

            await repo.AddLogAsync(log, stoppingToken);

            // Update item next execution time
            var interval = item.IntervalSeconds ?? 3600;
            item.LastExecutedAt = DateTimeOffset.UtcNow;
            item.NextExecutionAt = DateTimeOffset.UtcNow.AddSeconds(interval);
            await repo.UpdateAsync(item, stoppingToken);

            _logger.LogInformation("Completed automation {Name} in {DurationMs}ms (Success: {Success}). Next run: {NextRun}",
                item.Name, sw.ElapsedMilliseconds, success, item.NextExecutionAt);
        }
    }
}
