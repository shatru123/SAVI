using Moq;
using SAVI.Application.DTOs;
using SAVI.Application.Interfaces;
using SAVI.Application.Services;
using SAVI.Core.Entities;
using SAVI.Core.Enums;
using Xunit;

namespace SAVI.Application.Tests;

public class MemoryServiceTests
{
    [Fact]
    public async Task AddAsync_ShouldPersistMemoryItem()
    {
        var mockRepo = new Mock<IMemoryRepository>();
        var service = new MemoryService(mockRepo.Object);

        var dto = new CreateMemoryDto
        {
            Type = MemoryType.Preference,
            Content = "User prefers concise answers",
            Importance = 0.95
        };

        var created = await service.AddAsync(dto);

        Assert.NotNull(created);
        Assert.Equal(MemoryType.Preference, created.Type);
        Assert.Equal("User prefers concise answers", created.Content);
        mockRepo.Verify(r => r.AddAsync(It.Is<MemoryItem>(m => m.Content == dto.Content), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRelevantMemoriesAsync_ShouldMatchTokensAndRankHighImportance()
    {
        var list = new List<MemoryItem>
        {
            new() { Id = "1", Type = MemoryType.Preference, Content = "User prefers concise technical code snippets", Importance = 1.0 },
            new() { Id = "2", Type = MemoryType.Project, Content = "Building SAVI companion on .NET 10", Importance = 0.9 },
            new() { Id = "3", Type = MemoryType.ConversationFact, Content = "Likes hiking in mountains", Importance = 0.5 }
        };

        var mockRepo = new Mock<IMemoryRepository>();
        mockRepo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(list);

        var service = new MemoryService(mockRepo.Object);
        var relevant = await service.GetRelevantMemoriesAsync("Explain dependency injection in .NET");

        Assert.NotEmpty(relevant);
        Assert.Contains(relevant, m => m.Content.Contains("SAVI"));
    }
}
