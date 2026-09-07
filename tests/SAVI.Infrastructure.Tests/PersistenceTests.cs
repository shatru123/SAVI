using Microsoft.EntityFrameworkCore;
using SAVI.Core.Entities;
using SAVI.Core.Enums;
using SAVI.Infrastructure.Persistence;
using SAVI.Infrastructure.Repositories;
using Xunit;

namespace SAVI.Infrastructure.Tests;

public class PersistenceTests
{
    private SaviDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SaviDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new SaviDbContext(options);
    }

    [Fact]
    public async Task ConversationRepository_AddAndRetrieve_ShouldPersist()
    {
        using var db = CreateInMemoryDb();
        var repo = new ConversationRepository(db);

        var conv = new Conversation { Title = "Test AI Flow" };
        await repo.AddAsync(conv);

        var retrieved = await repo.GetByIdAsync(conv.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("Test AI Flow", retrieved.Title);
    }

    [Fact]
    public async Task MemoryRepository_ClearCategory_ShouldOnlyRemoveSelectedType()
    {
        using var db = CreateInMemoryDb();
        var repo = new MemoryRepository(db);

        await repo.AddAsync(new MemoryItem { Type = MemoryType.Preference, Content = "Pref 1" });
        await repo.AddAsync(new MemoryItem { Type = MemoryType.Preference, Content = "Pref 2" });
        await repo.AddAsync(new MemoryItem { Type = MemoryType.Project, Content = "Project SAVI" });

        await repo.ClearCategoryAsync(MemoryType.Preference);

        var remaining = await repo.GetAllAsync();
        Assert.Single(remaining);
        Assert.Equal(MemoryType.Project, remaining[0].Type);
    }
}
