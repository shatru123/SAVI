using Microsoft.AspNetCore.Identity;
using Moq;
using SAVI.Application.DTOs;
using SAVI.Application.Interfaces;
using SAVI.Application.Services;
using SAVI.Core.Constants;
using SAVI.Core.Entities;
using Xunit;

namespace SAVI.Application.Tests;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _mockUserRepo;
    private readonly PasswordHasher<User> _passwordHasher;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _mockUserRepo = new Mock<IUserRepository>();
        _passwordHasher = new PasswordHasher<User>();
        _authService = new AuthService(_mockUserRepo.Object, _passwordHasher);
    }

    [Fact]
    public async Task RegisterAsync_AlwaysAssignsUserRole_ZeroSelfPromotion()
    {
        var request = new RegisterRequestDto
        {
            DisplayName = "Normal User",
            Email = "user@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        User? savedUser = null;
        _mockUserRepo.Setup(r => r.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _mockUserRepo.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, ct) => savedUser = u)
            .Returns(Task.CompletedTask);

        var result = await _authService.RegisterAsync(request);

        Assert.True(result.Success);
        Assert.NotNull(result.User);
        Assert.Equal(SaviConstants.Roles.User, result.User.Role);
        Assert.NotNull(savedUser);
        Assert.Equal(SaviConstants.Roles.User, savedUser.Role);
        Assert.True(savedUser.IsActive);
        Assert.NotEmpty(savedUser.PasswordHash);
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateEmail_ReturnsError()
    {
        var request = new RegisterRequestDto
        {
            DisplayName = "Another User",
            Email = "existing@example.com",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        _mockUserRepo.Setup(r => r.GetByEmailAsync("existing@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = "u1", Email = "existing@example.com" });

        var result = await _authService.RegisterAsync(request);

        Assert.False(result.Success);
        Assert.Contains("already exists", result.Error, StringComparison.OrdinalIgnoreCase);
        _mockUserRepo.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WithShortPassword_ReturnsError()
    {
        var request = new RegisterRequestDto
        {
            DisplayName = "Test User",
            Email = "short@example.com",
            Password = "123",
            ConfirmPassword = "123"
        };

        var result = await _authService.RegisterAsync(request);

        Assert.False(result.Success);
        Assert.Contains("at least 6 characters", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegisterAsync_WithPasswordMismatch_ReturnsError()
    {
        var request = new RegisterRequestDto
        {
            DisplayName = "Test User",
            Email = "mismatch@example.com",
            Password = "Password123",
            ConfirmPassword = "Password456"
        };

        var result = await _authService.RegisterAsync(request);

        Assert.False(result.Success);
        Assert.Contains("do not match", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsSuccess()
    {
        var user = new User
        {
            Id = "user1",
            Email = "valid@example.com",
            DisplayName = "Valid User",
            Role = SaviConstants.Roles.User,
            IsActive = true
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, "CorrectPassword123");

        _mockUserRepo.Setup(r => r.GetByEmailAsync("valid@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _authService.LoginAsync("valid@example.com", "CorrectPassword123");

        Assert.True(result.Success);
        Assert.NotNull(result.User);
        Assert.Equal("valid@example.com", result.User.Email);
        _mockUserRepo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsError()
    {
        var user = new User
        {
            Id = "user1",
            Email = "valid@example.com",
            DisplayName = "Valid User",
            Role = SaviConstants.Roles.User,
            IsActive = true
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, "CorrectPassword123");

        _mockUserRepo.Setup(r => r.GetByEmailAsync("valid@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _authService.LoginAsync("valid@example.com", "WrongPassword999");

        Assert.False(result.Success);
        Assert.Contains("Invalid email or password", result.Error, StringComparison.OrdinalIgnoreCase);
        _mockUserRepo.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WithDeactivatedUser_ReturnsError()
    {
        var user = new User
        {
            Id = "user-deactivated",
            Email = "disabled@example.com",
            DisplayName = "Disabled User",
            Role = SaviConstants.Roles.User,
            IsActive = false
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, "Password123");

        _mockUserRepo.Setup(r => r.GetByEmailAsync("disabled@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _authService.LoginAsync("disabled@example.com", "Password123");

        Assert.False(result.Success);
        Assert.Contains("deactivated", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateUserAsync_ReturnsTrueOnlyForActiveUser()
    {
        _mockUserRepo.Setup(r => r.GetByIdAsync("active1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = "active1", IsActive = true });
        _mockUserRepo.Setup(r => r.GetByIdAsync("inactive1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = "inactive1", IsActive = false });
        _mockUserRepo.Setup(r => r.GetByIdAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        Assert.True(await _authService.ValidateUserAsync("active1"));
        Assert.False(await _authService.ValidateUserAsync("inactive1"));
        Assert.False(await _authService.ValidateUserAsync("missing"));
    }
}
