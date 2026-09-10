using SAVI.Core.Enums;

namespace SAVI.Core.Entities;

public sealed class UserSettings
{
    public string Id { get; set; } = "default";
    public PersonalityMode ActivePersonality { get; set; } = PersonalityMode.Friendly;
    public bool ReducedMotion { get; set; }
    public bool DarkMode { get; set; } = true;
    public bool VoiceEnabled { get; set; } = true;
    public string VoiceName { get; set; } = "default";
    public double SpeechRate { get; set; } = 1.0;
    public bool AutoApproveSafeTools { get; set; } = true;
    public string StoragePath { get; set; } = string.Empty;
    public string? UserId { get; set; }
}
