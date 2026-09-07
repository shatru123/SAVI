using Microsoft.EntityFrameworkCore;
using SAVI.Application.Interfaces;
using SAVI.Core.Entities;
using SAVI.Infrastructure.Persistence;

namespace SAVI.Infrastructure.Repositories;

public class SettingsRepository : ISettingsRepository
{
    private readonly SaviDbContext _db;

    public SettingsRepository(SaviDbContext db)
    {
        _db = db;
    }

    public async Task<UserSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _db.Settings.FirstOrDefaultAsync(s => s.Id == "default", cancellationToken);
        if (settings == null)
        {
            settings = new UserSettings
            {
                Id = "default",
                ActivePersonality = Core.Enums.PersonalityMode.Friendly,
                DarkMode = true,
                VoiceEnabled = true,
                SpeechRate = 1.0,
                AutoApproveSafeTools = true
            };
            await _db.Settings.AddAsync(settings, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }
        return settings;
    }

    public async Task UpdateSettingsAsync(UserSettings settings, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Settings.FirstOrDefaultAsync(s => s.Id == "default", cancellationToken);
        if (existing == null)
        {
            settings.Id = "default";
            await _db.Settings.AddAsync(settings, cancellationToken);
        }
        else
        {
            existing.ActivePersonality = settings.ActivePersonality;
            existing.ReducedMotion = settings.ReducedMotion;
            existing.DarkMode = settings.DarkMode;
            existing.VoiceEnabled = settings.VoiceEnabled;
            existing.VoiceName = settings.VoiceName;
            existing.SpeechRate = settings.SpeechRate;
            existing.AutoApproveSafeTools = settings.AutoApproveSafeTools;
            existing.StoragePath = settings.StoragePath;
            _db.Settings.Update(existing);
        }
        await _db.SaveChangesAsync(cancellationToken);
    }
}
