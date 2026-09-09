using Microsoft.AspNetCore.SignalR;
using SAVI.Application.DTOs;
using SAVI.Application.Interfaces;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;

namespace SAVI.Api.Hubs;

public class SaviHub : Hub
{
    private readonly IAgentOrchestrator _agentOrchestrator;
    private readonly IConversationService _conversationService;
    private readonly ITaskService _taskService;
    private readonly IVoiceConversationSession _voiceSession;

    public SaviHub(
        IAgentOrchestrator agentOrchestrator,
        IConversationService conversationService,
        ITaskService taskService,
        IVoiceConversationSession voiceSession)
    {
        _agentOrchestrator = agentOrchestrator;
        _conversationService = conversationService;
        _taskService = taskService;
        _voiceSession = voiceSession;
    }

    public async Task JoinConversation(string conversationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"conv_{conversationId}");
    }

    public async Task SendMessage(SendChatMessageRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Message))
        {
            throw new HubException("Message is required.");
        }

        if (request.Message.Length > SendChatMessageRequest.MaxMessageLength)
        {
            throw new HubException($"Message must be {SendChatMessageRequest.MaxMessageLength} characters or fewer.");
        }

        // 1. Instant deterministic acknowledgement (<100ms)
        await Clients.Caller.SendAsync("ReceiveVoiceState", VoiceState.Processing);
        await Clients.Caller.SendAsync("ReceiveAcknowledgement", new
        {
            ConversationId = request.ConversationId,
            Message = "Sure, Shatru. Checking that now...",
            Timestamp = DateTimeOffset.UtcNow
        });

        var caller = Clients.Caller;

        var agentRequest = new AgentRequest
        {
            Message = request.Message,
            ConversationId = request.ConversationId ?? string.Empty,
            PersonalityOverride = request.PersonalityOverride,
            VoiceActive = request.VoiceActive,
            ClientType = "SignalR",
            OnStepProgress = (step, label) =>
            {
                _ = caller.SendAsync("ReceiveExecutionStep", new { Step = step, Label = label });
            },
            OnExecutionEvent = (eventName, payload) =>
            {
                _ = caller.SendAsync("ReceiveExecutionEvent", new { Event = eventName, Payload = payload });
            }
        };

        var response = await _agentOrchestrator.ProcessAsync(agentRequest);

        // 2. Stream tokens in small chunks for responsive JARVIS experience
        var words = response.Message.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            var isLast = i == words.Length - 1;
            await Clients.Caller.SendAsync("ReceiveStreamToken", new StreamTokenDto
            {
                ConversationId = response.ConversationId,
                Token = words[i] + (isLast ? "" : " "),
                IsComplete = isLast,
                VoiceState = response.ActiveVoiceState
            });
            await Task.Delay(15);
        }

        // 3. Send final complete response object with sources and tool metadata
        await Clients.Caller.SendAsync("ReceiveAgentResponse", new ChatResponseDto
        {
            ConversationId = response.ConversationId,
            Message = response.Message,
            Success = response.Success,
            Sources = response.Sources,
            RequiresApproval = response.RequiresApproval,
            ApprovalActionId = response.PendingApprovalAction?.ActionId,
            ApprovalDescription = response.PendingApprovalAction?.Description,
            VoiceState = response.ActiveVoiceState,
            Confidence = response.Confidence,
            ActivityLogs = response.ActivityLogs,
            TaskId = response.TaskId
        });

        await Clients.Caller.SendAsync("ReceiveVoiceState", VoiceState.Idle);
    }

    public async Task ApproveAction(string conversationId, string actionId, bool approved)
    {
        var resumeRequest = new AgentRequest
        {
            Message = approved ? "Yes, I approve. Proceed." : "No, cancel this action.",
            ConversationId = conversationId,
            ApprovedActionId = actionId,
            ActionApproved = approved,
            ClientType = "SignalR"
        };

        var response = await _agentOrchestrator.ProcessAsync(resumeRequest);
        await Clients.Caller.SendAsync("ReceiveAgentResponse", new ChatResponseDto
        {
            ConversationId = response.ConversationId,
            Message = response.Message,
            Success = response.Success,
            Sources = response.Sources,
            VoiceState = response.ActiveVoiceState,
            Confidence = response.Confidence,
            ActivityLogs = response.ActivityLogs
        });
    }

    public async Task UpdateVoiceState(VoiceState state)
    {
        await Clients.Caller.SendAsync("ReceiveVoiceState", state);
    }

    // --- Real-time Voice Session Management ---

    public async Task StartVoiceSession(string conversationId)
    {
        var caller = Clients.Caller;

        _voiceSession.StateChanged += state =>
        {
            _ = caller.SendAsync("ReceiveVoiceState", state);
        };

        _voiceSession.VoiceEventEmitted += (evt, payload) =>
        {
            _ = caller.SendAsync("ReceiveVoiceEvent", new { Event = evt, Payload = payload });
        };

        await _voiceSession.StartAsync(conversationId, Context.ConnectionAborted);
        await caller.SendAsync("ReceiveVoiceSessionStarted", new
        {
            SessionId = _voiceSession.SessionId,
            ConversationId = conversationId
        });
    }

    public async Task StopVoiceSession()
    {
        await _voiceSession.StopAsync(Context.ConnectionAborted);
        await Clients.Caller.SendAsync("ReceiveVoiceSessionStopped", new
        {
            SessionId = _voiceSession.SessionId
        });
    }

    public async Task InterruptVoiceSession()
    {
        await _voiceSession.InterruptAsync(Context.ConnectionAborted);
    }

    public async Task ProcessVoiceUtterance(string text, bool isInterruption)
    {
        var result = await _voiceSession.ProcessUtteranceAsync(text, isInterruption, Context.ConnectionAborted);
        await Clients.Caller.SendAsync("ReceiveVoiceTurnResult", result);
    }

    public async Task SendVoicePartialTranscript(string interimText)
    {
        await Clients.Caller.SendAsync("ReceiveVoiceEvent", new
        {
            Event = "voice.transcript.partial",
            Payload = new { Text = interimText }
        });
    }
}
