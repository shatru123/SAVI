using SAVI.Application.DTOs;
using SAVI.Core.Entities;
using SAVI.Core.Enums;

namespace SAVI.Application.Interfaces;

public interface IConversationService
{
    Task<ConversationDetailDto> GetOrCreateAsync(string? id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConversationSummaryDto>> GetSummariesAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<ConversationDetailDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Message> AppendMessageAsync(string conversationId, MessageRole role, string content, MessageType type = MessageType.Text, string? sourcesJson = null, string? toolsJson = null, string? metadataJson = null, CancellationToken cancellationToken = default);
    Task RenameAsync(string id, string newTitle, CancellationToken cancellationToken = default);
    Task ArchiveAsync(string id, bool isArchived, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConversationSummaryDto>> SearchAsync(string query, CancellationToken cancellationToken = default);
    Task<string> ExportAsync(string id, CancellationToken cancellationToken = default);
}
