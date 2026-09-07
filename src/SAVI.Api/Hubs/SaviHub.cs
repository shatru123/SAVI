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

    public SaviHub(
        IAgentOrchestrator agentOrchestrator,
        IConversationService conversationService,
        ITaskService taskService)
    {
        _agentOrchestrator = agentOrchestrator;
        _conversationService = conversationService;
        _taskService = taskService;
    }

    public async Task JoinConversation(string conversationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"conv_{conversationId}");
    }

    public async Task SendMessage(SendChatMessageRequest request)
    {
        // 1. Notify client that SAVI is thinking
        await Clients.Caller.SendAsync("ReceiveVoiceState", VoiceState.Processing);

        var agentRequest = new AgentRequest
        {
            Message = request.Message,
            ConversationId = request.ConversationId ?? string.Empty,
            PersonalityOverride = request.PersonalityOverride,
            VoiceActive = request.VoiceActive,
            ClientType = "SignalR"
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
            await Task.Delay(20); // Smooth fluid typing cadence
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
}
