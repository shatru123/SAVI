using System.Diagnostics;
using System.Text.Json;
using SAVI.Agent.Planning;
using SAVI.Agent.Routing;
using SAVI.Application.DTOs;
using SAVI.Application.Interfaces;
using SAVI.Core.Constants;
using SAVI.Core.Entities;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Agent.Orchestration;

public class AgentOrchestrator : IAgentOrchestrator
{
    private readonly IContextBuilder _contextBuilder;
    private readonly IntentDetector _intentDetector;
    private readonly ExecutionPlanner _executionPlanner;
    private readonly IVerificationEngine _verificationEngine;
    private readonly IPersonalityEngine _personalityEngine;
    private readonly IMemoryExtractor _memoryExtractor;
    private readonly IConversationService _conversationService;
    private readonly IMemoryService _memoryService;
    private readonly ISettingsService _settingsService;
    private readonly IToolRegistry _toolRegistry;
    private readonly IPermissionGuard _permissionGuard;
    private readonly IProviderCache _providerCache;

    public AgentOrchestrator(
        IContextBuilder contextBuilder,
        IntentDetector intentDetector,
        ExecutionPlanner executionPlanner,
        IVerificationEngine verificationEngine,
        IPersonalityEngine personalityEngine,
        IMemoryExtractor memoryExtractor,
        IConversationService conversationService,
        IMemoryService memoryService,
        ISettingsService settingsService,
        IToolRegistry toolRegistry,
        IPermissionGuard permissionGuard,
        IProviderCache providerCache)
    {
        _contextBuilder = contextBuilder;
        _intentDetector = intentDetector;
        _executionPlanner = executionPlanner;
        _verificationEngine = verificationEngine;
        _personalityEngine = personalityEngine;
        _memoryExtractor = memoryExtractor;
        _conversationService = conversationService;
        _memoryService = memoryService;
        _settingsService = settingsService;
        _toolRegistry = toolRegistry;
        _permissionGuard = permissionGuard;
        _providerCache = providerCache;
    }

    public async Task<AgentResponse> ProcessAsync(AgentRequest request, CancellationToken cancellationToken = default)
    {
        var totalSw = Stopwatch.StartNew();
        var activityLogs = new List<string>();

        void LogActivity(string step)
        {
            activityLogs.Add($"{DateTime.UtcNow:HH:mm:ss} — {step}");
        }

        void EmitEvent(string eventName, object? payload = null)
        {
            request.OnExecutionEvent?.Invoke(eventName, payload);
        }

        EmitEvent("request.started", new { Timestamp = DateTimeOffset.UtcNow, Prompt = request.Message });
        request.OnStepProgress?.Invoke(1, "Analyzing task & intent");
        LogActivity("1. Request initialization & context retrieval");

        // Stage 1 & 2: Conversation & Context Retrieval
        var conv = await _conversationService.GetOrCreateAsync(request.ConversationId, cancellationToken);
        var conversationId = conv.Id;
        var context = await _contextBuilder.BuildContextAsync(request with { ConversationId = conversationId }, cancellationToken);

        await _conversationService.AppendMessageAsync(conversationId, MessageRole.User, request.Message, MessageType.Text, cancellationToken: cancellationToken);
        EmitEvent("context.ready", new { ConversationId = conversationId, MemoryCount = context.RelevantMemories.Count });

        // Stage 3 & 4: Intent Detection & Capability Routing
        var intent = _intentDetector.Detect(request.Message, context);
        EmitEvent("request.classified", new { Capability = intent.Capability, Operation = intent.Operation });
        LogActivity($"2. Routed capability '{intent.Capability}' (Operation: {intent.Operation})");

        // Handle Chit-Chat & System Greetings
        if (intent.Capability == "chitchat")
        {
            var reply = _personalityEngine.FormatChitChat(intent.Operation, request.Message);
            await _conversationService.AppendMessageAsync(conversationId, MessageRole.Assistant, reply, MessageType.Text, cancellationToken: cancellationToken);
            EmitEvent("response.completed", new { Message = reply });

            return new AgentResponse
            {
                Message = reply,
                ConversationId = conversationId,
                Success = true,
                Confidence = 1.0,
                ActiveVoiceState = VoiceState.Speaking,
                ActivityLogs = activityLogs
            };
        }

        // Handle Memory commands (remember & recall)
        if (intent.Capability == SaviConstants.Capabilities.Memory)
        {
            if (intent.Operation == "remember")
            {
                var contentToRemember = intent.Parameters.GetValueOrDefault("content") ?? request.Message;
                await _memoryService.AddAsync(new CreateMemoryDto
                {
                    Type = MemoryType.Preference,
                    Content = contentToRemember,
                    Importance = 1.0
                }, conversationId, cancellationToken);

                var confirmation = $"Got it, Shatru. I've saved that in long-term memory: \"{contentToRemember}\".";
                await _conversationService.AppendMessageAsync(conversationId, MessageRole.Assistant, confirmation, MessageType.Text, cancellationToken: cancellationToken);
                EmitEvent("response.completed", new { Message = confirmation });

                return new AgentResponse
                {
                    Message = confirmation,
                    ConversationId = conversationId,
                    Success = true,
                    Confidence = 1.0,
                    ActivityLogs = activityLogs
                };
            }

            if (intent.Operation == "recall")
            {
                var memories = context.RelevantMemories.Count > 0
                    ? context.RelevantMemories
                    : (await _memoryService.GetAllAsync(cancellationToken)).Select(m => new MemoryItem { Type = m.Type, Content = m.Content }).ToList();

                var reply = memories.Count == 0
                    ? "I don't have any saved memories for you yet. Tell me something like 'Remember that I prefer concise answers' and I'll keep it stored."
                    : "Here is what I remember:\n\n" +
                      string.Join("\n", memories.Select(m => $"• [{m.Type}] {m.Content}"));

                await _conversationService.AppendMessageAsync(conversationId, MessageRole.Assistant, reply, MessageType.Text, cancellationToken: cancellationToken);
                EmitEvent("response.completed", new { Message = reply });

                return new AgentResponse
                {
                    Message = reply,
                    ConversationId = conversationId,
                    Success = true,
                    Confidence = 1.0,
                    ActivityLogs = activityLogs
                };
            }
        }

        // Stage 5: Execution Planning
        request.OnStepProgress?.Invoke(2, "Routing to optimal capability providers");
        var plan = _executionPlanner.CreatePlan(intent, request);
        EmitEvent("provider.selected", new { Primary = plan.PrimaryProviders.Select(p => p.Name).ToList(), Policy = plan.Policy.ToString() });

        // Tool Execution Branch
        if (plan.RequiresTool && plan.ToolName != null && plan.ToolInput != null)
        {
            LogActivity($"Evaluating tool '{plan.ToolName}' permissions");
            var tool = _toolRegistry.GetTool(plan.ToolName);
            if (tool == null)
            {
                var notFound = $"Tool '{plan.ToolName}' is not available on this system.";
                return new AgentResponse { Message = notFound, ConversationId = conversationId, Success = false, ActivityLogs = activityLogs };
            }

            if (!await _permissionGuard.CanExecuteAsync(plan.ToolInput, cancellationToken))
            {
                var approvalReq = _permissionGuard.CreateApprovalRequest(plan.ToolInput);
                var approvalPrompt = _personalityEngine.FormatActionApprovalPrompt(approvalReq.Description, approvalReq.PermissionLevel);
                LogActivity($"Awaiting user approval for {approvalReq.PermissionLevel} action");

                await _conversationService.AppendMessageAsync(conversationId, MessageRole.Assistant, approvalPrompt, MessageType.ActionApproval, cancellationToken: cancellationToken);

                return new AgentResponse
                {
                    Message = approvalPrompt,
                    ConversationId = conversationId,
                    Success = true,
                    RequiresApproval = true,
                    PendingApprovalAction = approvalReq,
                    ActiveVoiceState = VoiceState.Idle,
                    ActivityLogs = activityLogs
                };
            }

            LogActivity($"Executing tool '{plan.ToolName}'");
            var toolResult = await tool.ExecuteAsync(plan.ToolInput, cancellationToken);
            var toolMsg = toolResult.Success ? toolResult.Output ?? "Tool execution completed." : $"Tool execution failed: {toolResult.ErrorMessage}";

            var userSettings = await _settingsService.GetSettingsAsync(cancellationToken);
            var formattedToolResponse = _personalityEngine.FormatResponse(toolMsg, userSettings.ActivePersonality, context.RelevantMemories, request.VoiceActive);

            var toolsJson = JsonSerializer.Serialize(new[] { toolResult });
            await _conversationService.AppendMessageAsync(conversationId, MessageRole.Assistant, formattedToolResponse, MessageType.Text, toolsJson: toolsJson, cancellationToken: cancellationToken);

            return new AgentResponse
            {
                Message = formattedToolResponse,
                ConversationId = conversationId,
                Success = toolResult.Success,
                ExecutedTools = new[] { toolResult },
                Confidence = toolResult.Success ? 1.0 : 0.0,
                ActivityLogs = activityLogs
            };
        }

        // Stage 6: Concurrent Provider Execution
        var taskRequest = new TaskRequest
        {
            Capability = intent.Capability,
            Operation = intent.Operation,
            Parameters = intent.Parameters,
            Prompt = request.Message,
            ConversationId = conversationId,
            Context = context
        };

        request.OnStepProgress?.Invoke(3, "Executing parallel provider queries");

        var providerResults = new List<ProviderResult>();

        // Execute primary providers concurrently using Task.WhenAll
        if (plan.PrimaryProviders.Count > 0)
        {
            LogActivity($"Querying {plan.PrimaryProviders.Count} primary provider(s) in parallel: {string.Join(", ", plan.PrimaryProviders.Select(p => p.Name))}");
            var primaryTasks = plan.PrimaryProviders.Select(p => ExecuteProviderSafelyAsync(p, taskRequest, LogActivity, EmitEvent, cancellationToken));
            var primaryBatch = await Task.WhenAll(primaryTasks);
            providerResults.AddRange(primaryBatch);
        }

        // Fallback / Verification execution
        var hasSuccessfulPrimary = providerResults.Any(r => r.Success);
        bool shouldRunVerification = (plan.Policy == VerificationPolicy.Verified) ||
                                     (!hasSuccessfulPrimary && plan.VerificationProviders.Count > 0);

        if (shouldRunVerification && plan.VerificationProviders.Count > 0)
        {
            var isFallback = !hasSuccessfulPrimary;
            LogActivity(isFallback
                ? $"Falling back to {plan.VerificationProviders.Count} secondary provider(s) in parallel: {string.Join(", ", plan.VerificationProviders.Select(p => p.Name))}"
                : $"Cross-verifying with {plan.VerificationProviders.Count} verification provider(s) in parallel: {string.Join(", ", plan.VerificationProviders.Select(p => p.Name))}");

            var verifyTasks = plan.VerificationProviders.Select(vp => ExecuteProviderSafelyAsync(vp, taskRequest, LogActivity, EmitEvent, cancellationToken));
            var verifyBatch = await Task.WhenAll(verifyTasks);
            providerResults.AddRange(verifyBatch);
        }

        // Stage 7: Verification Engine
        request.OnStepProgress?.Invoke(4, "Cross-verifying source accuracy & boundaries");
        LogActivity("Synthesizing and cross-verifying provider outputs");
        EmitEvent("verification.started");

        var verification = await _verificationEngine.VerifyAndCompareAsync(request.Message, providerResults, cancellationToken);
        EmitEvent("verification.completed", new { IsVerified = verification.IsVerified, Confidence = verification.Confidence });

        // Stage 8: Personality Engine Response Formatting
        var settings = await _settingsService.GetSettingsAsync(cancellationToken);
        var activePersonality = request.PersonalityOverride ?? settings.ActivePersonality;
        var finalContent = _personalityEngine.FormatResponse(verification.Synthesis, activePersonality, context.RelevantMemories, request.VoiceActive);

        // Stage 9: Persistence
        totalSw.Stop();
        LogActivity($"Completed in {totalSw.ElapsedMilliseconds} ms (Confidence: {verification.Confidence:P0})");

        var sourcesJson = JsonSerializer.Serialize(verification.Sources);
        var metadataJson = JsonSerializer.Serialize(activityLogs);
        await _conversationService.AppendMessageAsync(conversationId, MessageRole.Assistant, finalContent, MessageType.Text, sourcesJson: sourcesJson, metadataJson: metadataJson, cancellationToken: cancellationToken);

        EmitEvent("response.completed", new { Message = finalContent, TotalLatencyMs = totalSw.ElapsedMilliseconds });

        // Stage 10: Asynchronous Memory Extraction via Safe Task
        _ = Task.Run(async () =>
        {
            try
            {
                await _memoryExtractor.ExtractMemoriesAsync(request.Message, finalContent, conversationId, CancellationToken.None);
            }
            catch { }
        });

        return new AgentResponse
        {
            Message = finalContent,
            ConversationId = conversationId,
            Success = verification.IsVerified,
            Sources = verification.Sources,
            Confidence = verification.Confidence,
            ActiveVoiceState = request.VoiceActive ? VoiceState.Speaking : VoiceState.Idle,
            ActivityLogs = activityLogs,
            Telemetry = new Dictionary<string, string>
            {
                ["LatencyMs"] = totalSw.ElapsedMilliseconds.ToString(),
                ["Confidence"] = verification.Confidence.ToString("F2"),
                ["SourcesCount"] = verification.Sources.Count.ToString(),
                ["Capability"] = intent.Capability,
                ["Policy"] = plan.Policy.ToString()
            }
        };
    }

    private async Task<ProviderResult> ExecuteProviderSafelyAsync(
        ICapabilityProvider provider,
        TaskRequest request,
        Action<string> logActivity,
        Action<string, object?> emitEvent,
        CancellationToken cancellationToken)
    {
        // Provider-aware caching & request coalescing
        var cacheKey = $"{provider.Id}:{request.Capability}:{request.Operation}:{request.Prompt}".Trim().ToLowerInvariant();

        emitEvent("provider.started", new { ProviderId = provider.Id, ProviderName = provider.Name });
        var sw = Stopwatch.StartNew();

        try
        {
            var result = await _providerCache.GetOrExecuteAsync(
                provider.Id,
                cacheKey,
                provider.SupportsCaching ? provider.CacheTtl : TimeSpan.Zero,
                async ct =>
                {
                    using var timeoutCts = new CancellationTokenSource(provider.Timeout);
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                    return await provider.ExecuteAsync(request, linked.Token);
                },
                cancellationToken);

            sw.Stop();

            if (result.Success)
            {
                logActivity($"✓ {provider.Name} succeeded ({sw.ElapsedMilliseconds} ms)");
                emitEvent("provider.completed", new { ProviderId = provider.Id, Success = true, LatencyMs = sw.ElapsedMilliseconds });
            }
            else
            {
                logActivity($"✗ {provider.Name} failed: {result.Error} ({sw.ElapsedMilliseconds} ms)");
                emitEvent("provider.completed", new { ProviderId = provider.Id, Success = false, LatencyMs = sw.ElapsedMilliseconds, Error = result.Error });
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            logActivity($"✗ {provider.Name} timed out after {provider.Timeout.TotalMilliseconds} ms");
            emitEvent("provider.completed", new { ProviderId = provider.Id, Success = false, Error = "Timeout" });
            return ProviderResult.Failed(provider.Id, provider.Name, $"Provider timed out after {provider.Timeout.TotalMilliseconds} ms");
        }
        catch (Exception ex)
        {
            sw.Stop();
            logActivity($"✗ {provider.Name} error: {ex.Message}");
            emitEvent("provider.completed", new { ProviderId = provider.Id, Success = false, Error = ex.Message });
            return ProviderResult.Failed(provider.Id, provider.Name, ex.Message);
        }
    }
}
