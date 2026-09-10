using Microsoft.EntityFrameworkCore;
using SAVI.Application.Interfaces;
using SAVI.Core.Entities;
using SAVI.Infrastructure.Persistence;

namespace SAVI.Infrastructure.Repositories;

public class ConversationRepository : IConversationRepository
{
    private readonly SaviDbContext _db;

    public ConversationRepository(SaviDbContext db)
    {
        _db = db;
    }

    public async Task<Conversation?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var conv = await _db.Conversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (conv?.Messages != null)
        {
            conv.Messages = conv.Messages.OrderBy(m => m.Timestamp).ToList();
        }
        return conv;
    }

    public async Task<IReadOnlyList<Conversation>> GetAllAsync(string? userId = null, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var query = _db.Conversations.Include(c => c.Messages).AsQueryable();
        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(c => c.UserId == userId);
        }
        if (!includeArchived)
        {
            query = query.Where(c => !c.IsArchived);
        }
        var list = await query.ToListAsync(cancellationToken);
        return list.OrderByDescending(c => c.UpdatedAt).ToList();
    }

    public async Task<IReadOnlyList<Conversation>> SearchAsync(string query, string? userId = null, CancellationToken cancellationToken = default)
    {
        var qLower = query.ToLower();
        var q = _db.Conversations
            .Include(c => c.Messages)
            .Where(c => c.Title.ToLower().Contains(qLower) ||
                        c.Messages.Any(m => m.Content.ToLower().Contains(qLower)));

        if (!string.IsNullOrEmpty(userId))
        {
            q = q.Where(c => c.UserId == userId);
        }

        var list = await q.ToListAsync(cancellationToken);
        return list.OrderByDescending(c => c.UpdatedAt).ToList();
    }

    public async Task<int> GetCountAsync(string? userId = null, CancellationToken cancellationToken = default)
    {
        var q = _db.Conversations.AsQueryable();
        if (!string.IsNullOrEmpty(userId))
        {
            q = q.Where(c => c.UserId == userId);
        }
        return await q.CountAsync(cancellationToken);
    }

    public async Task<int> GetTotalMessagesCountAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Messages.CountAsync(cancellationToken);
    }

    public async Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        await _db.Conversations.AddAsync(conversation, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        _db.Conversations.Update(conversation);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (conv != null)
        {
            _db.Conversations.Remove(conv);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task AddMessageAsync(Message message, CancellationToken cancellationToken = default)
    {
        await _db.Messages.AddAsync(message, cancellationToken);

        var conv = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == message.ConversationId, cancellationToken);
        if (conv != null)
        {
            conv.UpdatedAt = DateTimeOffset.UtcNow;
            _db.Conversations.Update(conv);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Message>> GetMessagesAsync(string conversationId, int limit = 50, CancellationToken cancellationToken = default)
    {
        var messages = await _db.Messages
            .Where(m => m.ConversationId == conversationId)
            .ToListAsync(cancellationToken);

        return messages
            .OrderByDescending(m => m.Timestamp)
            .Take(limit)
            .OrderBy(m => m.Timestamp)
            .ToList();
    }
}
