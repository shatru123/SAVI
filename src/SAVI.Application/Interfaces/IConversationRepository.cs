using SAVI.Core.Entities;

namespace SAVI.Application.Interfaces;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Conversation>> GetAllAsync(string? userId = null, bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Conversation>> SearchAsync(string query, string? userId = null, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(string? userId = null, CancellationToken cancellationToken = default);
    Task<int> GetTotalMessagesCountAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default);
    Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task AddMessageAsync(Message message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Message>> GetMessagesAsync(string conversationId, int limit = 50, CancellationToken cancellationToken = default);
}
