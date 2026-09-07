using SAVI.Application.Interfaces;
using SAVI.Core.Entities;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;

namespace SAVI.Agent.Context;

public class ContextBuilder : IContextBuilder
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMemoryService _memoryService;

    public ContextBuilder(IConversationRepository conversationRepository, IMemoryService memoryService)
    {
        _conversationRepository = conversationRepository;
        _memoryService = memoryService;
    }

    public async Task<ContextPackage> BuildContextAsync(AgentRequest request, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Message> recentMessages = Array.Empty<Message>();
        string? summary = null;

        if (!string.IsNullOrWhiteSpace(request.ConversationId))
        {
            var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken);
            if (conversation != null)
            {
                summary = conversation.Summary;
                recentMessages = conversation.Messages
                    .OrderByDescending(m => m.Timestamp)
                    .Take(10)
                    .OrderBy(m => m.Timestamp)
                    .ToList();
            }
        }

        // Retrieve relevant long-term memories
        var relevantMemories = await _memoryService.GetRelevantMemoriesAsync(request.Message, cancellationToken);

        // Coreference query resolution
        string? resolvedQuery = null;
        var lower = request.Message.ToLowerInvariant().Trim();
        if ((lower.StartsWith("which one") || lower.StartsWith("open the second") || lower.StartsWith("what about it")) && recentMessages.Count > 0)
        {
            var lastAss = recentMessages.LastOrDefault(m => m.Role == Core.Enums.MessageRole.Assistant);
            if (lastAss != null)
            {
                resolvedQuery = $"{request.Message} (Context: {lastAss.Content[..Math.Min(120, lastAss.Content.Length)]})";
            }
        }

        return new ContextPackage
        {
            CurrentPrompt = request.Message,
            RecentMessages = recentMessages,
            ConversationSummary = summary,
            RelevantMemories = relevantMemories,
            ResolvedCoreferenceQuery = resolvedQuery,
            Intent = "DeterminedLater"
        };
    }
}
