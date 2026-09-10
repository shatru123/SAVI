using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SAVI.Application.DTOs;
using SAVI.Application.Interfaces;
using SAVI.Application.Services;
using SAVI.Core.Constants;
using SAVI.Core.Entities;
using Xunit;

namespace SAVI.Application.Tests;

public class AutomationServiceTests
{
    private static CurrentUserService CreateUser(string userId, string role)
    {
        var service = new CurrentUserService();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role),
            new(ClaimTypes.Name, $"User {userId}"),
            new(ClaimTypes.Email, $"{userId}@example.com")
        };
        service.SetUser(new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")));
        return service;
    }

    [Fact]
    public async Task CreateAsync_SetsUserId_And_Calculates_NextExecutionAt()
    {
        var mockRepo = new Mock<IAutomationRepository>();
        var currentUser = CreateUser("user-1", SaviConstants.Roles.User);
        var mockScopeFactory = new Mock<IServiceScopeFactory>();

        AutomationItem? savedItem = null;
        mockRepo.Setup(r => r.AddAsync(It.IsAny<AutomationItem>(), It.IsAny<CancellationToken>()))
            .Callback<AutomationItem, CancellationToken>((item, ct) => savedItem = item)
            .ReturnsAsync((AutomationItem item, CancellationToken ct) => item);

        var service = new AutomationService(mockRepo.Object, currentUser, mockScopeFactory.Object);

        var dto = new CreateAutomationDto(
            "Hourly Weather",
            "Fetches weather forecast",
            "Interval",
            3600,
            null,
            "Check weather in Tokyo",
            true
        );

        var result = await service.CreateAsync(dto);

        Assert.NotNull(result);
        Assert.NotNull(savedItem);
        Assert.Equal("user-1", savedItem.UserId);
        Assert.Equal("Hourly Weather", savedItem.Name);
        Assert.True(savedItem.IsEnabled);
        Assert.NotNull(savedItem.NextExecutionAt);
        Assert.True(savedItem.NextExecutionAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ToggleEnabledAsync_Toggles_State_And_Updates_NextExecutionAt()
    {
        var mockRepo = new Mock<IAutomationRepository>();
        var currentUser = CreateUser("user-1", SaviConstants.Roles.User);
        var mockScopeFactory = new Mock<IServiceScopeFactory>();

        var existing = new AutomationItem
        {
            Id = "auto-1",
            UserId = "user-1",
            Name = "Routine 1",
            Prompt = "Run test",
            IsEnabled = true,
            IntervalSeconds = 1800,
            NextExecutionAt = DateTimeOffset.UtcNow.AddMinutes(30)
        };

        mockRepo.Setup(r => r.GetByIdAsync("auto-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        mockRepo.Setup(r => r.UpdateAsync(It.IsAny<AutomationItem>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new AutomationService(mockRepo.Object, currentUser, mockScopeFactory.Object);

        // Toggle to disabled
        var toggled = await service.ToggleEnabledAsync("auto-1");
        Assert.True(toggled);
        Assert.False(existing.IsEnabled);
        Assert.Null(existing.NextExecutionAt);

        // Toggle back to enabled
        await service.ToggleEnabledAsync("auto-1");
        Assert.True(existing.IsEnabled);
        Assert.NotNull(existing.NextExecutionAt);
    }

    [Fact]
    public async Task GetAllAsync_UserA_Cannot_See_UserB_Automations()
    {
        var mockRepo = new Mock<IAutomationRepository>();
        var userB = CreateUser("user-B", SaviConstants.Roles.User);
        var mockScopeFactory = new Mock<IServiceScopeFactory>();

        mockRepo.Setup(r => r.GetAllAsync("user-B", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AutomationItem>());

        var service = new AutomationService(mockRepo.Object, userB, mockScopeFactory.Object);
        var list = await service.GetAllAsync();

        Assert.Empty(list);
        mockRepo.Verify(r => r.GetAllAsync("user-B", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TriggerNowAsync_Records_ExecutionLog()
    {
        var mockRepo = new Mock<IAutomationRepository>();
        var currentUser = CreateUser("user-1", SaviConstants.Roles.User);

        var serviceCollection = new ServiceCollection();
        var provider = serviceCollection.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var existing = new AutomationItem
        {
            Id = "auto-1",
            UserId = "user-1",
            Name = "Immediate Routine",
            Prompt = "Check memory",
            IsEnabled = true,
            IntervalSeconds = 3600
        };

        mockRepo.Setup(r => r.GetByIdAsync("auto-1", "user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        AutomationExecutionLog? capturedLog = null;
        mockRepo.Setup(r => r.AddLogAsync(It.IsAny<AutomationExecutionLog>(), It.IsAny<CancellationToken>()))
            .Callback<AutomationExecutionLog, CancellationToken>((log, ct) => capturedLog = log)
            .Returns(Task.CompletedTask);
        mockRepo.Setup(r => r.UpdateAsync(It.IsAny<AutomationItem>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new AutomationService(mockRepo.Object, currentUser, scopeFactory);

        var logResult = await service.TriggerNowAsync("auto-1");

        Assert.NotNull(logResult);
        Assert.True(logResult.Success);
        Assert.NotNull(capturedLog);
        Assert.Equal("auto-1", capturedLog.AutomationId);
        Assert.True(capturedLog.DurationMs >= 0);
        Assert.NotNull(existing.LastExecutedAt);
    }
}
