using Microsoft.EntityFrameworkCore;
using SAVI.Application.Interfaces;
using SAVI.Core.Entities;
using SAVI.Infrastructure.Persistence;

namespace SAVI.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly SaviDbContext _db;

    public UserRepository(SaviDbContext db)
    {
        _db = db;
    }

    public async Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _db.Users
            .Include(u => u.Conversations)
            .Include(u => u.MemoryItems)
            .Include(u => u.TaskItems)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return await _db.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(string? query = null, int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var q = _db.Users
            .Include(u => u.Conversations)
            .Include(u => u.MemoryItems)
            .Include(u => u.TaskItems)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var filter = query.Trim().ToLowerInvariant();
            q = q.Where(u => u.DisplayName.ToLower().Contains(filter) ||
                             u.Email.ToLower().Contains(filter) ||
                             u.Id.ToLower().Contains(filter));
        }

        return await q
            .OrderByDescending(u => u.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(string? query = null, CancellationToken cancellationToken = default)
    {
        var q = _db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var filter = query.Trim().ToLowerInvariant();
            q = q.Where(u => u.DisplayName.ToLower().Contains(filter) ||
                             u.Email.ToLower().Contains(filter) ||
                             u.Id.ToLower().Contains(filter));
        }
        return await q.CountAsync(cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _db.Users.AddAsync(user, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _db.Users.Update(user);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user != null)
        {
            _db.Users.Remove(user);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
