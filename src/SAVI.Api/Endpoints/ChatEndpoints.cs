using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Mvc;
using SAVI.Application.DTOs;
using SAVI.Application.Interfaces;
using SAVI.Core.Entities;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;

namespace SAVI.Api.Endpoints;

public static class ChatEndpoints
{
    public static void MapSaviEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1");

        // 1. Chat
        group.MapPost("/chat", async (
            [FromBody] SendChatMessageRequest request,
            IAgentOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            var agentReq = new AgentRequest
            {
                Message = request.Message,
                ConversationId = request.ConversationId ?? string.Empty,
                PersonalityOverride = request.PersonalityOverride,
                VoiceActive = request.VoiceActive,
                ApprovedActionId = request.ApprovedActionId,
                ActionApproved = request.ActionApproved,
                ClientType = "HttpApi"
            };

            var res = await orchestrator.ProcessAsync(agentReq, ct);

            return Results.Ok(new ChatResponseDto
            {
                ConversationId = res.ConversationId,
                Message = res.Message,
                Success = res.Success,
                Sources = res.Sources,
                RequiresApproval = res.RequiresApproval,
                ApprovalActionId = res.PendingApprovalAction?.ActionId,
                ApprovalDescription = res.PendingApprovalAction?.Description,
                VoiceState = res.ActiveVoiceState,
                Confidence = res.Confidence,
                ActivityLogs = res.ActivityLogs,
                TaskId = res.TaskId
            });
        });

        // 2. Conversations
        group.MapGet("/conversations", async (IConversationService service, [FromQuery] bool? includeArchived, CancellationToken ct) =>
        {
            var list = await service.GetSummariesAsync(includeArchived ?? false, ct);
            return Results.Ok(list);
        });

        group.MapGet("/conversations/{id}", async (string id, IConversationService service, CancellationToken ct) =>
        {
            var conv = await service.GetByIdAsync(id, ct);
            return conv != null ? Results.Ok(conv) : Results.NotFound();
        });

        group.MapPut("/conversations/{id}/rename", async (string id, [FromBody] RenameConversationDto dto, IConversationService service, CancellationToken ct) =>
        {
            await service.RenameAsync(id, dto.Title, ct);
            return Results.Ok(new { success = true });
        });

        group.MapDelete("/conversations/{id}", async (string id, IConversationService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.Ok(new { success = true });
        });

        group.MapGet("/conversations/{id}/export", async (string id, IConversationService service, CancellationToken ct) =>
        {
            var json = await service.ExportAsync(id, ct);
            return Results.Content(json, "application/json");
        });

        group.MapGet("/conversations/search", async ([FromQuery] string? q, IConversationService service, CancellationToken ct) =>
        {
            var list = string.IsNullOrWhiteSpace(q) ? await service.GetSummariesAsync(false, ct) : await service.SearchAsync(q, ct);
            return Results.Ok(list);
        });

        // 3. Memory
        group.MapGet("/memory", async (IMemoryService service, [FromQuery] MemoryType? type, [FromQuery] string? q, CancellationToken ct) =>
        {
            if (!string.IsNullOrWhiteSpace(q))
            {
                var search = await service.SearchAsync(q, ct);
                return Results.Ok(search);
            }
            if (type.HasValue)
            {
                var byType = await service.GetByCategoryAsync(type.Value, ct);
                return Results.Ok(byType);
            }
            var all = await service.GetAllAsync(ct);
            return Results.Ok(all);
        });

        group.MapPost("/memory", async ([FromBody] CreateMemoryDto dto, IMemoryService service, CancellationToken ct) =>
        {
            var created = await service.AddAsync(dto, null, ct);
            return Results.Created($"/api/v1/memory/{created.Id}", created);
        });

        group.MapPut("/memory/{id}", async (string id, [FromBody] UpdateMemoryDto dto, IMemoryService service, CancellationToken ct) =>
        {
            var updated = await service.UpdateAsync(id, dto, ct);
            return updated != null ? Results.Ok(updated) : Results.NotFound();
        });

        group.MapDelete("/memory/{id}", async (string id, IMemoryService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return Results.Ok(new { success = true });
        });

        group.MapDelete("/memory/category/{type}", async (MemoryType type, IMemoryService service, CancellationToken ct) =>
        {
            await service.ClearCategoryAsync(type, ct);
            return Results.Ok(new { success = true });
        });

        group.MapDelete("/memory/all", async (IMemoryService service, CancellationToken ct) =>
        {
            await service.ClearAllAsync(ct);
            return Results.Ok(new { success = true });
        });

        // 4. Tasks
        group.MapGet("/tasks", async (ITaskService service, [FromQuery] TaskState? state, CancellationToken ct) =>
        {
            var list = await service.GetAllAsync(state, ct);
            return Results.Ok(list);
        });

        group.MapGet("/tasks/{id}", async (string id, ITaskService service, CancellationToken ct) =>
        {
            var task = await service.GetByIdAsync(id, ct);
            return task != null ? Results.Ok(task) : Results.NotFound();
        });

        group.MapPost("/tasks/{id}/approve", async (string id, [FromBody] ApproveTaskStepDto dto, ITaskService service, CancellationToken ct) =>
        {
            var updated = await service.ApproveStepAsync(id, dto.StepId, dto.Approved, ct);
            return Results.Ok(updated);
        });

        group.MapPost("/tasks/{id}/cancel", async (string id, ITaskService service, CancellationToken ct) =>
        {
            await service.CancelTaskAsync(id, ct);
            return Results.Ok(new { success = true });
        });

        // 5. Providers
        group.MapGet("/providers", async (IProviderRegistry registry, CancellationToken ct) =>
        {
            var metadata = await registry.GetMetadataAsync(ct);
            return Results.Ok(metadata);
        });

        group.MapPost("/providers", async (
            [FromBody] CreateGenericProviderDto dto,
            IProviderDefinitionRepository repo,
            IProviderRegistry registry,
            HttpClient httpClient,
            CancellationToken ct) =>
        {
            var def = new ApiProviderDefinition
            {
                Id = Guid.NewGuid().ToString(),
                Name = dto.Name,
                Capability = dto.Capability,
                Method = dto.Method,
                Endpoint = dto.Endpoint,
                HeadersJson = dto.Headers != null ? System.Text.Json.JsonSerializer.Serialize(dto.Headers) : null,
                ParametersJson = dto.Parameters != null ? System.Text.Json.JsonSerializer.Serialize(dto.Parameters) : null,
                ResponseMappingJson = dto.ResponseMapping != null ? System.Text.Json.JsonSerializer.Serialize(dto.ResponseMapping) : null,
                Priority = dto.Priority,
                RateLimitPerMin = dto.RateLimitPerMin,
                TimeoutSeconds = dto.TimeoutSeconds
            };

            await repo.AddAsync(def, ct);
            var provider = new SAVI.Infrastructure.Providers.Generic.GenericHttpApiProvider(httpClient, def);
            registry.Register(provider);

            return Results.Created($"/api/v1/providers/{def.Id}", def);
        });

        // 6. Settings
        group.MapGet("/settings", async (ISettingsService service, CancellationToken ct) =>
        {
            var settings = await service.GetSettingsAsync(ct);
            return Results.Ok(settings);
        });

        group.MapPut("/settings", async ([FromBody] UserSettingsDto dto, ISettingsService service, CancellationToken ct) =>
        {
            var updated = await service.UpdateSettingsAsync(dto, ct);
            return Results.Ok(updated);
        });

        // 7. Audit Logs
        group.MapGet("/audit", async (IAuditService service, [FromQuery] int? limit, CancellationToken ct) =>
        {
            var logs = await service.GetRecentLogsAsync(limit.HasValue && limit.Value > 0 ? limit.Value : 50, ct);
            return Results.Ok(logs);
        });

        // 8. Diagnostics
        group.MapGet("/diagnostics", async (
            IConversationRepository convRepo,
            IMemoryRepository memRepo,
            IProviderRegistry providerRegistry,
            CancellationToken ct) =>
        {
            var proc = Process.GetCurrentProcess();
            var convCount = (await convRepo.GetAllAsync(true, ct)).Count;
            var memCount = await memRepo.GetCountAsync(ct);

            return Results.Ok(new DiagnosticsDto
            {
                SystemName = "SAVI",
                Version = "1.0.0",
                OperatingSystem = RuntimeInformation.OSDescription,
                Architecture = RuntimeInformation.OSArchitecture.ToString(),
                DotnetVersion = RuntimeInformation.FrameworkDescription,
                Uptime = DateTime.UtcNow - proc.StartTime.ToUniversalTime(),
                MemoryUsedBytes = proc.WorkingSet64,
                ActiveConversationsCount = convCount,
                MemoryItemsCount = memCount,
                RegisteredProvidersCount = providerRegistry.GetAll().Count,
                InternetAvailable = true
            });
        });
    }
}
