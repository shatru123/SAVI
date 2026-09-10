using SAVI.Application.Interfaces;
using SAVI.Core.Entities;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;

namespace SAVI.Agent.Context;

public class ContextBuilder : IContextBuilder
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IMemoryService _memoryService;
    private readonly ICurrentUserService? _currentUserService;

    public ContextBuilder(
        IConversationRepository conversationRepository,
        IMemoryService memoryService,
        ICurrentUserService? currentUserService = null)
    {
        _conversationRepository = conversationRepository;
        _memoryService = memoryService;
        _currentUserService = currentUserService;
    }

    public async Task<ContextPackage> BuildContextAsync(AgentRequest request, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Message> recentMessages = Array.Empty<Message>();
        IReadOnlyList<Message> relevantHistorical = Array.Empty<Message>();
        string? summary = null;

        if (!string.IsNullOrWhiteSpace(request.ConversationId))
        {
            var conversation = await _conversationRepository.GetByIdAsync(request.ConversationId, cancellationToken);
            if (conversation != null)
            {
                summary = conversation.Summary;
                var allMessages = conversation.Messages.OrderBy(m => m.Timestamp).ToList();

                // Detailed recent messages (last 10)
                recentMessages = allMessages
                    .TakeLast(10)
                    .ToList();

                // Rolling Context Compression: if conversation exceeds 10 turns, summarize older messages
                if (allMessages.Count > 10)
                {
                    var olderMessages = allMessages.Take(allMessages.Count - 10).ToList();
                    if (string.IsNullOrWhiteSpace(summary))
                    {
                        var keyTopics = olderMessages
                            .Where(m => m.Role == Core.Enums.MessageRole.User)
                            .Select(m => m.Content.Length > 60 ? m.Content[..57] + "..." : m.Content)
                            .TakeLast(3);
                        summary = $"Prior discussion covered: {string.Join("; ", keyTopics)}";
                    }

                    // Retrieve older messages if relevant to current query tokens
                    var promptTokens = request.Message.Split(new[] { ' ', ',', '.', '?' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(t => t.Length >= 4)
                        .Select(t => t.ToLowerInvariant())
                        .ToList();

                    if (promptTokens.Count > 0)
                    {
                        relevantHistorical = olderMessages
                            .Where(m => promptTokens.Any(t => m.Content.Contains(t, StringComparison.OrdinalIgnoreCase)))
                            .TakeLast(2)
                            .ToList();
                    }
                }
            }
        }

        // Retrieve relevant long-term memories (scoped to current user)
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
            RelevantHistoricalMessages = relevantHistorical,
            ResolvedCoreferenceQuery = resolvedQuery,
            UserName = _currentUserService?.DisplayName ?? "Operator",
            UserId = _currentUserService?.UserId,
            Intent = "DeterminedLater"
        };
    }
}
