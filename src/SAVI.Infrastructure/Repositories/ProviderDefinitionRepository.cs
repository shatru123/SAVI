using Microsoft.EntityFrameworkCore;
using SAVI.Application.Interfaces;
using SAVI.Core.Entities;
using SAVI.Infrastructure.Persistence;

namespace SAVI.Infrastructure.Repositories;

public class ProviderDefinitionRepository : IProviderDefinitionRepository
{
    private readonly SaviDbContext _db;

    public ProviderDefinitionRepository(SaviDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ApiProviderDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.ApiProviders.OrderBy(p => p.Priority).ToListAsync(cancellationToken);
    }

    public async Task<ApiProviderDefinition?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _db.ApiProviders.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task AddAsync(ApiProviderDefinition definition, CancellationToken cancellationToken = default)
    {
        await _db.ApiProviders.AddAsync(definition, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ApiProviderDefinition definition, CancellationToken cancellationToken = default)
    {
        _db.ApiProviders.Update(definition);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var item = await _db.ApiProviders.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (item != null)
        {
            _db.ApiProviders.Remove(item);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
