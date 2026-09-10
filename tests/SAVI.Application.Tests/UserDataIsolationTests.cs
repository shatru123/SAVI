using System.Security.Claims;
using Moq;
using SAVI.Application.Interfaces;
using SAVI.Application.Services;
using SAVI.Core.Constants;
using SAVI.Core.Entities;
using SAVI.Core.Enums;
using Xunit;

namespace SAVI.Application.Tests;

public class UserDataIsolationTests
{
    private readonly Mock<IConversationRepository> _mockConvRepo;

    public UserDataIsolationTests()
    {
        _mockConvRepo = new Mock<IConversationRepository>();
    }

    private static CurrentUserService CreateCurrentUserService(string userId, string role, string name = "Test User")
    {
        var service = new CurrentUserService();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Email, $"{userId}@example.com")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        service.SetUser(new ClaimsPrincipal(identity));
        return service;
    }

    [Fact]
    public async Task GetOrCreateAsync_SetsUserIdToAuthenticatedUser()
    {
        var currentUser = CreateCurrentUserService("user-123", SaviConstants.Roles.User);
        var convService = new ConversationService(_mockConvRepo.Object, currentUser);

        Conversation? savedConv = null;
        _mockConvRepo.Setup(r => r.AddAsync(It.IsAny<Conversation>(), It.IsAny<CancellationToken>()))
            .Callback<Conversation, CancellationToken>((c, ct) => savedConv = c)
            .Returns(Task.CompletedTask);

        var detail = await convService.GetOrCreateAsync(null);

        Assert.NotNull(detail);
        Assert.NotNull(savedConv);
        Assert.Equal("user-123", savedConv.UserId);
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserBAccessesUserAConversation_ReturnsNull()
    {
        var convA = new Conversation
        {
            Id = "conv-a",
            UserId = "user-A",
            Title = "User A Private Conversation"
        };
        _mockConvRepo.Setup(r => r.GetByIdAsync("conv-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(convA);

        // User B tries to view User A's conversation
        var userB = CreateCurrentUserService("user-B", SaviConstants.Roles.User);
        var convService = new ConversationService(_mockConvRepo.Object, userB);

        var result = await convService.GetByIdAsync("conv-a");

        Assert.Null(result); // Cross-user boundary enforced!
    }

    [Fact]
    public async Task GetByIdAsync_WhenOwnerAccessesUserAConversation_Succeeds()
    {
        var convA = new Conversation
        {
            Id = "conv-a",
            UserId = "user-A",
            Title = "User A Private Conversation"
        };
        _mockConvRepo.Setup(r => r.GetByIdAsync("conv-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(convA);

        // Owner accesses conversation
        var owner = CreateCurrentUserService("owner-1", SaviConstants.Roles.Owner);
        var convService = new ConversationService(_mockConvRepo.Object, owner);

        var result = await convService.GetByIdAsync("conv-a");

        Assert.NotNull(result);
        Assert.Equal("User A Private Conversation", result.Title);
    }

    [Fact]
    public async Task GetSummariesAsync_ScopesQueryToCurrentUserId()
    {
        var userA = CreateCurrentUserService("user-A", SaviConstants.Roles.User);
        var convService = new ConversationService(_mockConvRepo.Object, userA);

        _mockConvRepo.Setup(r => r.GetAllAsync("user-A", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Conversation>
            {
                new() { Id = "conv1", UserId = "user-A", Title = "Chat 1" }
            });

        var summaries = await convService.GetSummariesAsync();

        Assert.Single(summaries);
        _mockConvRepo.Verify(r => r.GetAllAsync("user-A", false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AppendMessageAsync_WhenUserBAccessesUserAConversation_ThrowsUnauthorized()
    {
        var convA = new Conversation
        {
            Id = "conv-a",
            UserId = "user-A",
            Title = "User A Private Conversation"
        };
        _mockConvRepo.Setup(r => r.GetByIdAsync("conv-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(convA);

        var userB = CreateCurrentUserService("user-B", SaviConstants.Roles.User);
        var convService = new ConversationService(_mockConvRepo.Object, userB);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            convService.AppendMessageAsync("conv-a", MessageRole.User, "Injected message from B"));
    }

    [Fact]
    public async Task RenameAsync_WhenUserBAccessesUserAConversation_ThrowsUnauthorized()
    {
        var convA = new Conversation
        {
            Id = "conv-a",
            UserId = "user-A",
            Title = "User A Private Conversation"
        };
        _mockConvRepo.Setup(r => r.GetByIdAsync("conv-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(convA);

        var userB = CreateCurrentUserService("user-B", SaviConstants.Roles.User);
        var convService = new ConversationService(_mockConvRepo.Object, userB);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            convService.RenameAsync("conv-a", "Malicious Title Rename"));
    }

    [Fact]
    public async Task DeleteAsync_WhenUserBAccessesUserAConversation_ThrowsUnauthorized()
    {
        var convA = new Conversation
        {
            Id = "conv-a",
            UserId = "user-A",
            Title = "User A Private Conversation"
        };
        _mockConvRepo.Setup(r => r.GetByIdAsync("conv-a", It.IsAny<CancellationToken>()))
            .ReturnsAsync(convA);

        var userB = CreateCurrentUserService("user-B", SaviConstants.Roles.User);
        var convService = new ConversationService(_mockConvRepo.Object, userB);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            convService.DeleteAsync("conv-a"));
    }
}
