using SAVI.Application.DTOs;
using SAVI.Application.Interfaces;
using SAVI.Core.Entities;

namespace SAVI.Application.Services;

public class SettingsService : ISettingsService
{
    private readonly ISettingsRepository _repository;

    public SettingsService(ISettingsRepository repository)
    {
        _repository = repository;
    }

    public async Task<UserSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _repository.GetSettingsAsync(cancellationToken);
        return new UserSettingsDto
        {
            ActivePersonality = settings.ActivePersonality,
            ReducedMotion = settings.ReducedMotion,
            DarkMode = settings.DarkMode,
            VoiceEnabled = settings.VoiceEnabled,
            VoiceName = settings.VoiceName,
            SpeechRate = settings.SpeechRate,
            AutoApproveSafeTools = settings.AutoApproveSafeTools,
            StoragePath = settings.StoragePath
        };
    }

    public async Task<UserSettingsDto> UpdateSettingsAsync(UserSettingsDto dto, CancellationToken cancellationToken = default)
    {
        var settings = new UserSettings
        {
            Id = "default",
            ActivePersonality = dto.ActivePersonality,
            ReducedMotion = dto.ReducedMotion,
            DarkMode = dto.DarkMode,
            VoiceEnabled = dto.VoiceEnabled,
            VoiceName = dto.VoiceName,
            SpeechRate = dto.SpeechRate,
            AutoApproveSafeTools = dto.AutoApproveSafeTools,
            StoragePath = dto.StoragePath
        };

        await _repository.UpdateSettingsAsync(settings, cancellationToken);
        return dto;
    }
}
