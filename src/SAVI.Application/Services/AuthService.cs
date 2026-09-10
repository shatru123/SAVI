using Microsoft.AspNetCore.Identity;
using SAVI.Application.DTOs;
using SAVI.Application.Interfaces;
using SAVI.Core.Constants;
using SAVI.Core.Entities;

namespace SAVI.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher<User> passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<(bool Success, string? Error, UserDto? User)> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return (false, "Email and password are required.", null);
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user == null)
        {
            return (false, "Invalid email or password.", null);
        }

        if (!user.IsActive)
        {
            return (false, "Account has been deactivated. Please contact the system administrator.", null);
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return (false, "Invalid email or password.", null);
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);

        return (true, null, MapToDto(user));
    }

    public async Task<(bool Success, string? Error, UserDto? User)> RegisterAsync(
        RegisterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return (false, "Email is required.", null);
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return (false, "Display name is required.", null);
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return (false, "Password is required.", null);
        }

        if (request.Password.Length < 6)
        {
            return (false, "Password must be at least 6 characters long.", null);
        }

        if (!string.Equals(request.Password, request.ConfirmPassword))
        {
            return (false, "Passwords do not match.", null);
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existing = await _userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
        if (existing != null)
        {
            return (false, "An account with this email address already exists.", null);
        }

        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = normalizedEmail,
            DisplayName = request.DisplayName.Trim(),
            Role = SaviConstants.Roles.User, // Always User on registration. Zero self-promotion.
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

        await _userRepository.AddAsync(user, cancellationToken);

        return (true, null, MapToDto(user));
    }

    public async Task<bool> ValidateUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return user != null && user.IsActive;
    }

    private static UserDto MapToDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Role = user.Role,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            IsActive = user.IsActive
        };
    }
}
