using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using SAVI.Agent.Planning;
using SAVI.Agent.Routing;
using SAVI.Agent.Synthesis;
using SAVI.Agent.Understanding;
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
    private readonly IQueryUnderstandingService _queryUnderstandingService;
    private readonly IAnswerSynthesisService _answerSynthesisService;
    private readonly EvidenceAggregator _evidenceAggregator;
    private readonly IVoiceResponseFormatter? _voiceResponseFormatter;

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
        IProviderCache providerCache,
        IQueryUnderstandingService? queryUnderstandingService = null,
        IAnswerSynthesisService? answerSynthesisService = null,
        EvidenceAggregator? evidenceAggregator = null,
        IVoiceResponseFormatter? voiceResponseFormatter = null)
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
        _queryUnderstandingService = queryUnderstandingService ?? new Understanding.QueryUnderstandingService();
        _answerSynthesisService = answerSynthesisService ?? new Synthesis.AnswerSynthesisService();
        _evidenceAggregator = evidenceAggregator ?? new Synthesis.EvidenceAggregator();
        _voiceResponseFormatter = voiceResponseFormatter;
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
        var analysis = _queryUnderstandingService.Analyze(request.Message, context);
        var intent = _intentDetector.Detect(request.Message, context);
        EmitEvent("request.classified", new { Capability = intent.Capability, Operation = intent.Operation, IsTechnical = analysis.IsTechnical, Topic = analysis.Topic });
        LogActivity($"2. Analyzed query '{analysis.Topic}' (Domain: {analysis.Domain}) -> Routed capability '{intent.Capability}' (Operation: {intent.Operation})");

        if (intent.Capability == SaviConstants.Capabilities.Weather &&
            string.IsNullOrWhiteSpace(intent.Parameters.GetValueOrDefault("city")) &&
            string.IsNullOrWhiteSpace(intent.Parameters.GetValueOrDefault("location")))
        {
            const string clarification = "Which location should I check the weather for?";
            await _conversationService.AppendMessageAsync(conversationId, MessageRole.Assistant, clarification, MessageType.Text, cancellationToken: cancellationToken);
            return new AgentResponse
            {
                Message = clarification,
                VoiceFriendlyMessage = clarification,
                ConversationId = conversationId,
                Success = true,
                Confidence = 1.0,
                ActivityLogs = activityLogs
            };
        }

        // Handle Voice Control Commands (Stop, Repeat, Continue, Wait, Go Back, Keep It Short, etc.)
        if (intent.Capability == "voice_control")
        {
            string reply = intent.Operation switch
            {
                "stop" => "Stopped. I'm listening.",
                "wait" => "Yep?",
                "go_back" => "Sure.",
                "repeat" => context.RecentMessages.LastOrDefault(m => m.Role == MessageRole.Assistant)?.Content ?? "I'm ready when you are, Shatru.",
                "continue" => "Continuing from where we left off.",
                "keep_it_short" => HandleKeepItShort(context),
                "first_item" => HandleFirstItem(context),
                "clarify_note" => HandleClarifyNote(context),
                _ => "Understood."
            };

            var voiceReply = _voiceResponseFormatter?.FormatForSpeech(reply) ?? reply;
            await _conversationService.AppendMessageAsync(conversationId, MessageRole.Assistant, reply, MessageType.Text, cancellationToken: cancellationToken);
            EmitEvent("response.completed", new { Message = reply });

            return new AgentResponse
            {
                Message = reply,
                VoiceFriendlyMessage = voiceReply,
                ConversationId = conversationId,
                Success = true,
                Confidence = 1.0,
                ActiveVoiceState = VoiceState.Speaking,
                ActivityLogs = activityLogs
            };
        }

        // Handle Chit-Chat & System Greetings
        if (intent.Capability == "chitchat")
        {
            var reply = _personalityEngine.FormatChitChat(intent.Operation, request.Message);
            var voiceReply = _voiceResponseFormatter?.FormatForSpeech(reply) ?? reply;
            await _conversationService.AppendMessageAsync(conversationId, MessageRole.Assistant, reply, MessageType.Text, cancellationToken: cancellationToken);
            EmitEvent("response.completed", new { Message = reply });

            return new AgentResponse
            {
                Message = reply,
                VoiceFriendlyMessage = voiceReply,
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
        var plan = _executionPlanner.CreatePlan(intent, request, analysis: analysis);
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
        var taskParameters = new Dictionary<string, string>(intent.Parameters, StringComparer.OrdinalIgnoreCase)
        {
            ["canonical_query"] = analysis.CanonicalLookupQuery,
            ["requirements"] = string.Join(",", analysis.InformationRequirements),
            ["domain"] = analysis.Domain,
            ["requires_freshness"] = analysis.RequiresFreshness.ToString()
        };
        if (analysis.RetrievalQueries.Count > 0)
        {
            taskParameters["retrieval_query"] = analysis.RetrievalQueries[0];
        }

        var taskRequest = new TaskRequest
        {
            Capability = intent.Capability,
            Operation = intent.Operation,
            Parameters = taskParameters,
            Prompt = request.Message,
            ConversationId = conversationId,
            Context = context,
            Analysis = analysis
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
                                     analysis.RequiresFreshness ||
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

        // Stage 7: Evidence Aggregation & Answer Synthesis
        request.OnStepProgress?.Invoke(4, "Aggregating provider evidence & synthesizing natural answer");
        LogActivity("Aggregating evidence from providers and synthesizing natural answer");
        EmitEvent("verification.started");

        var evidenceList = _evidenceAggregator.Aggregate(providerResults);
        var synthesized = await _answerSynthesisService.SynthesizeAsync(
            request.Message,
            analysis,
            evidenceList,
            context,
            request.VoiceActive,
            cancellationToken);

        var verification = await _verificationEngine.VerifyAndCompareAsync(request.Message, providerResults, cancellationToken);
        var isVerified = synthesized.IsVerified && verification.IsVerified;
        var confidence = Math.Max(synthesized.Confidence, verification.Confidence);
        var combinedSources = synthesized.Sources.Concat(verification.Sources)
            .GroupBy(s => string.IsNullOrWhiteSpace(s.Url) ? s.SourceName : s.Url)
            .Select(g => g.First())
            .ToList();

        EmitEvent("verification.completed", new { IsVerified = isVerified, Confidence = confidence });

        // Stage 8: Personality Engine Response Formatting
        var settings = await _settingsService.GetSettingsAsync(cancellationToken);
        var activePersonality = request.PersonalityOverride ?? settings.ActivePersonality;

        var baseContent = !string.IsNullOrWhiteSpace(synthesized.MainContent)
            ? synthesized.MainContent
            : verification.Synthesis;

        var finalContent = _personalityEngine.FormatResponse(baseContent, activePersonality, context.RelevantMemories, request.VoiceActive);

        // Stage 9: Persistence
        totalSw.Stop();
        LogActivity($"Completed in {totalSw.ElapsedMilliseconds} ms (Confidence: {confidence:P0})");

        var sourcesJson = JsonSerializer.Serialize(combinedSources);
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

        var voiceFriendlyText = !string.IsNullOrWhiteSpace(synthesized.VoiceContent)
            ? synthesized.VoiceContent
            : (_voiceResponseFormatter?.FormatForSpeech(finalContent, intent.Capability) ?? finalContent);

        return new AgentResponse
        {
            Message = finalContent,
            VoiceFriendlyMessage = voiceFriendlyText,
            ConversationId = conversationId,
            Success = isVerified,
            Sources = combinedSources,
            Confidence = confidence,
            ActiveVoiceState = request.VoiceActive ? VoiceState.Speaking : VoiceState.Idle,
            ActivityLogs = activityLogs,
            Telemetry = new Dictionary<string, string>
            {
                ["LatencyMs"] = totalSw.ElapsedMilliseconds.ToString(),
                ["Confidence"] = confidence.ToString("F2"),
                ["SourcesCount"] = combinedSources.Count.ToString(),
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

            var extracted = _evidenceAggregator.Aggregate(new[] { result }).FirstOrDefault();
            return result with
            {
                Capability = request.Capability,
                Title = extracted?.Title ?? string.Empty,
                RelevantPassages = extracted?.RelevantPassages ?? Array.Empty<string>(),
                Latency = sw.Elapsed,
                AuthorityScore = provider.AuthorityLevel,
                FreshnessScore = provider.Category switch
                {
                    ProviderCategory.LocalDeterministic => 1.0,
                    ProviderCategory.SpecializedPublicApi => 0.95,
                    ProviderCategory.WebSearch => 0.90,
                    ProviderCategory.KnowledgeBase => 0.80,
                    ProviderCategory.ReasoningSynthesis => 0.65,
                    _ => 0.75
                },
                IsDeterministic = provider.Category == ProviderCategory.LocalDeterministic
            };
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
            return ProviderResult.Failed(provider.Id, provider.Name, ex.Message) with
            {
                Capability = request.Capability,
                Latency = sw.Elapsed
            };
        }
    }

    private static string HandleKeepItShort(ContextPackage context)
    {
        var lastAssistantReply = context.RecentMessages.LastOrDefault(m => m.Role == MessageRole.Assistant)?.Content ?? "";
        return ExtractFirstSentence(lastAssistantReply) ?? "Understood. I’ll keep the answer concise.";
    }

    private static string HandleFirstItem(ContextPackage context)
    {
        var lastAssistantReply = context.RecentMessages.LastOrDefault(m => m.Role == MessageRole.Assistant)?.Content ?? "";
        var firstItem = lastAssistantReply
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => Regex.IsMatch(line, @"^(?:[-*•]|\d+[.)])\s*") ||
                                    Regex.IsMatch(line, @"^[A-Za-z][A-Za-z\s-]{0,24}:\s+\S"));
        return string.IsNullOrWhiteSpace(firstItem)
            ? ExtractFirstSentence(lastAssistantReply) ?? "I couldn't find a separate first item in the previous answer."
            : Regex.Replace(Regex.Replace(firstItem, @"^(?:[-*•]|\d+[.)])\s*", ""), @"^[A-Za-z][A-Za-z\s-]{0,24}:\s+", "").Trim();
    }

    private static string HandleClarifyNote(ContextPackage context)
    {
        var lastAssistantReply = context.RecentMessages.LastOrDefault(m => m.Role == MessageRole.Assistant)?.Content ?? "";
        return ExtractFirstSentence(lastAssistantReply) ?? "I don't have enough context to identify that note.";
    }

    private static string? ExtractFirstSentence(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var sentence = Regex.Match(text.Trim(), @"^(.+?[.!?])(?:\s|$)", RegexOptions.Singleline).Groups[1].Value;
        return string.IsNullOrWhiteSpace(sentence) ? text.Trim() : sentence.Trim();
    }
}
