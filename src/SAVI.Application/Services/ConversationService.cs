using System.Text.Json;
using SAVI.Application.DTOs;
using SAVI.Application.Interfaces;
using SAVI.Core.Entities;
using SAVI.Core.Enums;
using SAVI.Core.ValueObjects;

namespace SAVI.Application.Services;

public class ConversationService : IConversationService
{
    private readonly IConversationRepository _repository;
    private readonly ICurrentUserService? _currentUserService;

    public ConversationService(
        IConversationRepository repository,
        ICurrentUserService? currentUserService = null)
    {
        _repository = repository;
        _currentUserService = currentUserService;
    }

    public async Task<ConversationDetailDto> GetOrCreateAsync(string? id, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            var existing = await _repository.GetByIdAsync(id, cancellationToken);
            if (existing != null)
            {
                // Enforce user isolation: non-owners cannot access another user's conversation
                if (_currentUserService != null && _currentUserService.IsAuthenticated && !_currentUserService.IsOwner)
                {
                    if (!string.IsNullOrEmpty(existing.UserId) && existing.UserId != _currentUserService.UserId)
                    {
                        existing = null;
                    }
                }

                if (existing != null)
                {
                    return MapToDetail(existing);
                }
            }
        }

        var newConv = new Conversation
        {
            Id = Guid.NewGuid().ToString(),
            UserId = _currentUserService?.UserId,
            Title = "New Conversation",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _repository.AddAsync(newConv, cancellationToken);
        return MapToDetail(newConv);
    }

    public async Task<IReadOnlyList<ConversationSummaryDto>> GetSummariesAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService?.UserId;
        var list = await _repository.GetAllAsync(userId, includeArchived, cancellationToken);
        return list.Select(c => new ConversationSummaryDto
        {
            Id = c.Id,
            Title = c.Title,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
            IsArchived = c.IsArchived,
            MessageCount = c.Messages?.Count ?? 0,
            Summary = c.Summary
        }).ToList();
    }

    public async Task<ConversationDetailDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var conv = await _repository.GetByIdAsync(id, cancellationToken);
        if (conv == null)
        {
            return null;
        }

        if (_currentUserService != null && _currentUserService.IsAuthenticated && !_currentUserService.IsOwner)
        {
            if (!string.IsNullOrEmpty(conv.UserId) && conv.UserId != _currentUserService.UserId)
            {
                return null;
            }
        }

        return MapToDetail(conv);
    }

    public async Task<Message> AppendMessageAsync(
        string conversationId,
        MessageRole role,
        string content,
        MessageType type = MessageType.Text,
        string? sourcesJson = null,
        string? toolsJson = null,
        string? metadataJson = null,
        CancellationToken cancellationToken = default)
    {
        var conv = await _repository.GetByIdAsync(conversationId, cancellationToken);
        if (conv != null && _currentUserService != null && _currentUserService.IsAuthenticated && !_currentUserService.IsOwner)
        {
            if (!string.IsNullOrEmpty(conv.UserId) && conv.UserId != _currentUserService.UserId)
            {
                throw new UnauthorizedAccessException("Cannot append messages to another user's conversation.");
            }
        }

        var message = new Message
        {
            Id = Guid.NewGuid().ToString(),
            ConversationId = conversationId,
            Role = role,
            Content = content,
            MessageType = type,
            SourcesJson = sourcesJson,
            ToolExecutionsJson = toolsJson,
            MetadataJson = metadataJson,
            Timestamp = DateTimeOffset.UtcNow
        };

        await _repository.AddMessageAsync(message, cancellationToken);

        // Auto-generate a title from the first user message if title is default
        if (conv != null && (conv.Title == "New Conversation" || string.IsNullOrWhiteSpace(conv.Title)) && role == MessageRole.User)
        {
            var snippet = content.Trim();
            if (snippet.Length > 36)
            {
                snippet = snippet[..33] + "...";
            }
            conv.Title = snippet;
            conv.UpdatedAt = DateTimeOffset.UtcNow;
            await _repository.UpdateAsync(conv, cancellationToken);
        }

        return message;
    }

    public async Task RenameAsync(string id, string newTitle, CancellationToken cancellationToken = default)
    {
        var conv = await _repository.GetByIdAsync(id, cancellationToken);
        if (conv != null)
        {
            if (_currentUserService != null && _currentUserService.IsAuthenticated && !_currentUserService.IsOwner)
            {
                if (!string.IsNullOrEmpty(conv.UserId) && conv.UserId != _currentUserService.UserId)
                {
                    throw new UnauthorizedAccessException("Cannot modify another user's conversation.");
                }
            }

            conv.Title = newTitle;
            conv.UpdatedAt = DateTimeOffset.UtcNow;
            await _repository.UpdateAsync(conv, cancellationToken);
        }
    }

    public async Task ArchiveAsync(string id, bool isArchived, CancellationToken cancellationToken = default)
    {
        var conv = await _repository.GetByIdAsync(id, cancellationToken);
        if (conv != null)
        {
            if (_currentUserService != null && _currentUserService.IsAuthenticated && !_currentUserService.IsOwner)
            {
                if (!string.IsNullOrEmpty(conv.UserId) && conv.UserId != _currentUserService.UserId)
                {
                    throw new UnauthorizedAccessException("Cannot archive another user's conversation.");
                }
            }

            conv.IsArchived = isArchived;
            conv.UpdatedAt = DateTimeOffset.UtcNow;
            await _repository.UpdateAsync(conv, cancellationToken);
        }
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var conv = await _repository.GetByIdAsync(id, cancellationToken);
        if (conv != null)
        {
            if (_currentUserService != null && _currentUserService.IsAuthenticated && !_currentUserService.IsOwner)
            {
                if (!string.IsNullOrEmpty(conv.UserId) && conv.UserId != _currentUserService.UserId)
                {
                    throw new UnauthorizedAccessException("Cannot delete another user's conversation.");
                }
            }

            await _repository.DeleteAsync(id, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ConversationSummaryDto>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService?.UserId;
        var list = await _repository.SearchAsync(query, userId, cancellationToken);
        return list.Select(c => new ConversationSummaryDto
        {
            Id = c.Id,
            Title = c.Title,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
            IsArchived = c.IsArchived,
            MessageCount = c.Messages?.Count ?? 0,
            Summary = c.Summary
        }).ToList();
    }

    public async Task<string> ExportAsync(string id, CancellationToken cancellationToken = default)
    {
        var conv = await _repository.GetByIdAsync(id, cancellationToken);
        if (conv == null)
            return "{}";

        var detail = MapToDetail(conv);
        return JsonSerializer.Serialize(detail, new JsonSerializerOptions { WriteIndented = true });
    }

    private static ConversationDetailDto MapToDetail(Conversation c)
    {
        return new ConversationDetailDto
        {
            Id = c.Id,
            Title = c.Title,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
            IsArchived = c.IsArchived,
            Summary = c.Summary,
            Messages = c.Messages?.Select(m => new MessageDto
            {
                Id = m.Id,
                Role = m.Role,
                Content = m.Content,
                Timestamp = m.Timestamp,
                MessageType = m.MessageType,
                Sources = string.IsNullOrWhiteSpace(m.SourcesJson) ? null : JsonSerializer.Deserialize<List<SourceReference>>(m.SourcesJson),
                ToolExecutions = string.IsNullOrWhiteSpace(m.ToolExecutionsJson) ? null : JsonSerializer.Deserialize<List<ToolExecutionResult>>(m.ToolExecutionsJson),
                ActivityLogs = string.IsNullOrWhiteSpace(m.MetadataJson) ? null : JsonSerializer.Deserialize<List<string>>(m.MetadataJson)
            }).ToList() ?? new List<MessageDto>()
        };
    }
}
