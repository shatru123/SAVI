namespace SAVI.Application.Interfaces;

public enum CapabilityLevel
{
    Public = 0,
    Authenticated = 1,
    Owner = 2
}

public enum UserCapability
{
    BasicChat = 0,
    PublicSearch = 1,
    Weather = 2,
    Currency = 3,
    Calculator = 4,
    SelfKnowledge = 5,
    Conversions = 6,
    PublicKnowledge = 7,

    PersistentHistory = 10,
    Memory = 11,
    Tasks = 12,
    Agent = 13,
    VoiceHistory = 14,
    Files = 15,
    Automations = 16,
    Proactive = 17,

    Admin = 20
}

public interface IUserCapabilityService
{
    bool CanAccess(UserCapability capability);
    CapabilityLevel GetRequiredLevel(UserCapability capability);
    string GetGatingTitle(UserCapability capability);
    string GetGatingMessage(UserCapability capability);
}
