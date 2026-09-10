using SAVI.Application.DTOs;

namespace SAVI.Application.Interfaces;

public interface IAuthService
{
    Task<(bool Success, string? Error, UserDto? User)> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error, UserDto? User)> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<bool> ValidateUserAsync(string userId, CancellationToken cancellationToken = default);
}
