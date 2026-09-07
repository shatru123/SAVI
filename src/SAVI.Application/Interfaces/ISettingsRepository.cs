using SAVI.Core.Entities;

namespace SAVI.Application.Interfaces;

public interface ISettingsRepository
{
    Task<UserSettings> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task UpdateSettingsAsync(UserSettings settings, CancellationToken cancellationToken = default);
}
