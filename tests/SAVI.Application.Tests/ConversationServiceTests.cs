using Moq;
using SAVI.Application.Interfaces;
using SAVI.Application.Services;
using SAVI.Core.Entities;
using SAVI.Core.Enums;
using Xunit;

namespace SAVI.Application.Tests;

public class ConversationServiceTests
{
    [Fact]
    public async Task GetOrCreateAsync_WithNullId_ShouldCreateNewConversation()
    {
        var mockRepo = new Mock<IConversationRepository>();
        var service = new ConversationService(mockRepo.Object);

        var result = await service.GetOrCreateAsync(null);

        Assert.NotNull(result);
        Assert.Equal("New Conversation", result.Title);
        mockRepo.Verify(r => r.AddAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AppendMessageAsync_FirstUserMessage_ShouldUpdateTitleFromContent()
    {
        var conv = new Conversation { Id = "conv1", Title = "New Conversation" };
        var mockRepo = new Mock<IConversationRepository>();
        mockRepo.Setup(r => r.GetByIdAsync("conv1", It.IsAny<CancellationToken>())).ReturnsAsync(conv);

        var service = new ConversationService(mockRepo.Object);
        await service.AppendMessageAsync("conv1", MessageRole.User, "What is the weather in Tokyo today?");

        Assert.Contains("What is the weather", conv.Title);
        mockRepo.Verify(r => r.UpdateAsync(conv, It.IsAny<CancellationToken>()), Times.Once);
    }
}
