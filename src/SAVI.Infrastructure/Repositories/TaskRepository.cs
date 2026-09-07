using Microsoft.EntityFrameworkCore;
using SAVI.Application.Interfaces;
using SAVI.Core.Entities;
using SAVI.Core.Enums;
using SAVI.Infrastructure.Persistence;

namespace SAVI.Infrastructure.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly SaviDbContext _db;

    public TaskRepository(SaviDbContext db)
    {
        _db = db;
    }

    public async Task<TaskItem?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<TaskItem>> GetAllAsync(TaskState? state = null, CancellationToken cancellationToken = default)
    {
        var query = _db.TaskItems.AsQueryable();
        if (state.HasValue)
        {
            query = query.Where(t => t.State == state.Value);
        }
        var list = await query.ToListAsync(cancellationToken);
        return list.OrderByDescending(t => t.UpdatedAt).ToList();
    }

    public async Task AddAsync(TaskItem item, CancellationToken cancellationToken = default)
    {
        await _db.TaskItems.AddAsync(item, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TaskItem item, CancellationToken cancellationToken = default)
    {
        _db.TaskItems.Update(item);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var item = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (item != null)
        {
            _db.TaskItems.Remove(item);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
