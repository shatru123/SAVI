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
        IPermissionGuard permissionGuard)
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
    }

    public async Task<AgentResponse> ProcessAsync(AgentRequest request, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var activityLogs = new List<string>();

        void LogActivity(string step)
        {
            activityLogs.Add($"{DateTime.UtcNow:HH:mm:ss} — {step}");
        }

        LogActivity("Understanding request & retrieving context");

        // 1. Ensure conversation exists
        var conv = await _conversationService.GetOrCreateAsync(request.ConversationId, cancellationToken);
        var conversationId = conv.Id;

        // 2. Build Context
        var context = await _contextBuilder.BuildContextAsync(request with { ConversationId = conversationId }, cancellationToken);

        // 3. Append user message to persistent history
        await _conversationService.AppendMessageAsync(conversationId, MessageRole.User, request.Message, MessageType.Text, cancellationToken: cancellationToken);

        // 4. Intent Detection
        LogActivity("Analyzing intent & capability routing");
        var intent = _intentDetector.Detect(request.Message, context);

        // 5. Chit-Chat / Greetings / Listening Checks
        if (intent.Capability == "chitchat")
        {
            var reply = _personalityEngine.FormatChitChat(intent.Operation, request.Message);
            await _conversationService.AppendMessageAsync(conversationId, MessageRole.Assistant, reply, MessageType.Text, cancellationToken: cancellationToken);

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

        // 6. Memory specific commands (remember & recall)
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

                var confirmation = $"Got it, Shatru. I've stored that in long-term memory: \"{contentToRemember}\".";
                await _conversationService.AppendMessageAsync(conversationId, MessageRole.Assistant, confirmation, MessageType.Text, cancellationToken: cancellationToken);

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

        // 7. Execution Planning
        LogActivity($"Selecting providers for '{intent.Capability}'");
        var plan = _executionPlanner.CreatePlan(intent, request);

        // 8. Tool Execution
        if (plan.RequiresTool && plan.ToolName != null && plan.ToolInput != null)
        {
            LogActivity($"Evaluating tool '{plan.ToolName}' permissions");
            var tool = _toolRegistry.GetTool(plan.ToolName);
            if (tool == null)
            {
                var notFound = $"Tool '{plan.ToolName}' is not available on this system.";
                return new AgentResponse { Message = notFound, ConversationId = conversationId, Success = false, ActivityLogs = activityLogs };
            }

            // Check if approval is needed
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

            var toolMsg = toolResult.Success
                ? toolResult.Output ?? "Tool execution completed."
                : $"Tool execution failed: {toolResult.ErrorMessage}";

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

        // 9. Provider Execution
        var providerResults = new List<ProviderResult>();
        var taskRequest = new TaskRequest
        {
            Capability = intent.Capability,
            Operation = intent.Operation,
            Parameters = intent.Parameters,
            Prompt = request.Message,
            ConversationId = conversationId,
            Context = context
        };

        foreach (var p in plan.PrimaryProviders)
        {
            LogActivity($"Querying provider: {p.Name}");
            var res = await p.ExecuteAsync(taskRequest, cancellationToken);
            providerResults.Add(res);
        }

        // Query verification provider if primary returned and verification provider exists
        if (providerResults.Any(r => r.Success) && plan.VerificationProviders.Count > 0)
        {
            foreach (var vp in plan.VerificationProviders)
            {
                LogActivity($"Cross-verifying with: {vp.Name}");
                var vres = await vp.ExecuteAsync(taskRequest, cancellationToken);
                providerResults.Add(vres);
            }
        }

        // 10. Verification Engine
        LogActivity("Synthesizing and verifying source results");
        var verification = await _verificationEngine.VerifyAndCompareAsync(request.Message, providerResults, cancellationToken);

        // 11. Personality Engine Response Formatting
        var settings = await _settingsService.GetSettingsAsync(cancellationToken);
        var activePersonality = request.PersonalityOverride ?? settings.ActivePersonality;
        var finalContent = _personalityEngine.FormatResponse(verification.Synthesis, activePersonality, context.RelevantMemories, request.VoiceActive);

        // 12. Asynchronous Memory Extraction
        _ = Task.Run(async () =>
        {
            try
            {
                await _memoryExtractor.ExtractMemoriesAsync(request.Message, finalContent, conversationId, CancellationToken.None);
            }
            catch { }
        });

        // 13. Persist assistant response
        var sourcesJson = JsonSerializer.Serialize(verification.Sources);
        await _conversationService.AppendMessageAsync(conversationId, MessageRole.Assistant, finalContent, MessageType.Text, sourcesJson: sourcesJson, cancellationToken: cancellationToken);

        sw.Stop();
        LogActivity($"Completed in {sw.ElapsedMilliseconds} ms (Confidence: {verification.Confidence:P0})");

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
                ["LatencyMs"] = sw.ElapsedMilliseconds.ToString(),
                ["Confidence"] = verification.Confidence.ToString("F2"),
                ["SourcesCount"] = verification.Sources.Count.ToString(),
                ["Capability"] = intent.Capability
            }
        };
    }
}
