using SAVI.Application.DTOs;

namespace SAVI.Application.Interfaces;

public interface ISettingsService
{
    Task<UserSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<UserSettingsDto> UpdateSettingsAsync(UserSettingsDto dto, CancellationToken cancellationToken = default);
}
