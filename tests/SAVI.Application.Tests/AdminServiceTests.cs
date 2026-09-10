using System.Security.Claims;
using Moq;
using SAVI.Application.Interfaces;
using SAVI.Application.Services;
using SAVI.Core.Constants;
using SAVI.Core.Entities;
using SAVI.Core.Enums;
using Xunit;

namespace SAVI.Application.Tests;

public class AdminServiceTests
{
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly Mock<IConversationRepository> _mockConvRepo;
    private readonly Mock<IMemoryRepository> _mockMemoryRepo;
    private readonly Mock<ITaskRepository> _mockTaskRepo;
    private readonly Mock<IAuditRepository> _mockAuditRepo;

    public AdminServiceTests()
    {
        _mockUserRepo = new Mock<IUserRepository>();
        _mockConvRepo = new Mock<IConversationRepository>();
        _mockMemoryRepo = new Mock<IMemoryRepository>();
        _mockTaskRepo = new Mock<ITaskRepository>();
        _mockAuditRepo = new Mock<IAuditRepository>();
    }

    private static CurrentUserService CreateCurrentUserService(string userId, string role)
    {
        var service = new CurrentUserService();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role),
            new(ClaimTypes.Name, role == SaviConstants.Roles.Owner ? "Shatrughna" : "Normal User"),
            new(ClaimTypes.Email, $"{userId}@example.com")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        service.SetUser(new ClaimsPrincipal(identity));
        return service;
    }

    [Fact]
    public async Task GetPlatformMetricsAsync_WhenCalledByNonOwner_ThrowsUnauthorized()
    {
        var normalUser = CreateCurrentUserService("user-1", SaviConstants.Roles.User);
        var adminService = new AdminService(
            _mockUserRepo.Object,
            _mockConvRepo.Object,
            _mockMemoryRepo.Object,
            _mockTaskRepo.Object,
            _mockAuditRepo.Object,
            normalUser);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            adminService.GetPlatformMetricsAsync());
    }

    [Fact]
    public async Task GetPlatformMetricsAsync_WhenCalledByOwner_ReturnsAggregatedMetrics()
    {
        var owner = CreateCurrentUserService("owner-1", SaviConstants.Roles.Owner);
        var adminService = new AdminService(
            _mockUserRepo.Object,
            _mockConvRepo.Object,
            _mockMemoryRepo.Object,
            _mockTaskRepo.Object,
            _mockAuditRepo.Object,
            owner);

        _mockUserRepo.Setup(r => r.GetCountAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(10);
        _mockUserRepo.Setup(r => r.GetAllAsync(null, 0, 10000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<User>
            {
                new() { Id = "u1", IsActive = true },
                new() { Id = "u2", IsActive = false }
            });
        _mockConvRepo.Setup(r => r.GetCountAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(25);
        _mockConvRepo.Setup(r => r.GetTotalMessagesCountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(100);
        _mockMemoryRepo.Setup(r => r.GetCountAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(50);
        _mockTaskRepo.Setup(r => r.GetAllAsync(null, null, It.IsAny<CancellationToken>())).ReturnsAsync(new List<TaskItem>());

        var metrics = await adminService.GetPlatformMetricsAsync();

        Assert.NotNull(metrics);
        Assert.Equal(10, metrics.TotalUsers);
        Assert.Equal(1, metrics.ActiveUsers);
        Assert.Equal(25, metrics.TotalConversations);
        Assert.Equal(100, metrics.TotalMessages);
        Assert.Equal(50, metrics.TotalMemoryItems);
        Assert.Equal("Operational", metrics.SystemStatus);
    }

    [Fact]
    public async Task GetUsersAsync_WhenCalledByNonOwner_ThrowsUnauthorized()
    {
        var normalUser = CreateCurrentUserService("user-1", SaviConstants.Roles.User);
        var adminService = new AdminService(
            _mockUserRepo.Object,
            _mockConvRepo.Object,
            _mockMemoryRepo.Object,
            _mockTaskRepo.Object,
            _mockAuditRepo.Object,
            normalUser);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            adminService.GetUsersAsync(null, 1, 10));
    }

    [Fact]
    public async Task SetUserStatusAsync_WhenCalledByOwner_UpdatesStatusAndLogsAudit()
    {
        var owner = CreateCurrentUserService("owner-1", SaviConstants.Roles.Owner);
        var adminService = new AdminService(
            _mockUserRepo.Object,
            _mockConvRepo.Object,
            _mockMemoryRepo.Object,
            _mockTaskRepo.Object,
            _mockAuditRepo.Object,
            owner);

        var targetUser = new User
        {
            Id = "target-1",
            Email = "target@example.com",
            Role = SaviConstants.Roles.User,
            IsActive = true
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync("target-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);

        var success = await adminService.SetUserStatusAsync("target-1", false);

        Assert.True(success);
        Assert.False(targetUser.IsActive);
        _mockUserRepo.Verify(r => r.UpdateAsync(targetUser, It.IsAny<CancellationToken>()), Times.Once);
        _mockAuditRepo.Verify(r => r.AddAsync(It.Is<AuditLogEntry>(a =>
            a.TargetUserId == "target-1" &&
            a.Action == "UserDeactivated" &&
            a.PermissionLevel == PermissionLevel.Dangerous), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetUserStatusAsync_WhenAttemptingToDeactivateOwner_ThrowsInvalidOperation()
    {
        var owner = CreateCurrentUserService("owner-1", SaviConstants.Roles.Owner);
        var adminService = new AdminService(
            _mockUserRepo.Object,
            _mockConvRepo.Object,
            _mockMemoryRepo.Object,
            _mockTaskRepo.Object,
            _mockAuditRepo.Object,
            owner);

        var targetOwner = new User
        {
            Id = "owner-account",
            Email = "owner@example.com",
            Role = SaviConstants.Roles.Owner,
            IsActive = true
        };

        _mockUserRepo.Setup(r => r.GetByIdAsync("owner-account", It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetOwner);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            adminService.SetUserStatusAsync("owner-account", false));
    }

    [Fact]
    public async Task GetConversationDetailsForAdminAsync_ReturnsReadOnlyDetail()
    {
        var owner = CreateCurrentUserService("owner-1", SaviConstants.Roles.Owner);
        var adminService = new AdminService(
            _mockUserRepo.Object,
            _mockConvRepo.Object,
            _mockMemoryRepo.Object,
            _mockTaskRepo.Object,
            _mockAuditRepo.Object,
            owner);

        var conv = new Conversation
        {
            Id = "conv-100",
            UserId = "user-100",
            Title = "Secret Chat",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Messages = new List<Message>
            {
                new() { Id = "m1", Role = MessageRole.User, Content = "Hello SAVI" },
                new() { Id = "m2", Role = MessageRole.Assistant, Content = "Hello User" }
            }
        };

        _mockConvRepo.Setup(r => r.GetByIdAsync("conv-100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(conv);
        _mockUserRepo.Setup(r => r.GetByIdAsync("user-100", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = "user-100", DisplayName = "Alice" });

        var detail = await adminService.GetConversationDetailsForAdminAsync("conv-100");

        Assert.NotNull(detail);
        Assert.Equal("conv-100", detail.ConversationId);
        Assert.Equal("Alice", detail.UserDisplayName);
        Assert.Equal(2, detail.Messages.Count);
    }
}
