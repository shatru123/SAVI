using SAVI.Application.Interfaces;

namespace SAVI.Application.Services;

public class UserCapabilityService : IUserCapabilityService
{
    private readonly ICurrentUserService _currentUserService;

    public UserCapabilityService(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public CapabilityLevel GetRequiredLevel(UserCapability capability)
    {
        return capability switch
        {
            UserCapability.BasicChat => CapabilityLevel.Public,
            UserCapability.PublicSearch => CapabilityLevel.Public,
            UserCapability.Weather => CapabilityLevel.Public,
            UserCapability.Currency => CapabilityLevel.Public,
            UserCapability.Calculator => CapabilityLevel.Public,
            UserCapability.SelfKnowledge => CapabilityLevel.Public,
            UserCapability.Conversions => CapabilityLevel.Public,
            UserCapability.PublicKnowledge => CapabilityLevel.Public,

            UserCapability.PersistentHistory => CapabilityLevel.Authenticated,
            UserCapability.Memory => CapabilityLevel.Authenticated,
            UserCapability.Tasks => CapabilityLevel.Authenticated,
            UserCapability.Agent => CapabilityLevel.Authenticated,
            UserCapability.VoiceHistory => CapabilityLevel.Authenticated,
            UserCapability.Files => CapabilityLevel.Authenticated,
            UserCapability.Automations => CapabilityLevel.Authenticated,
            UserCapability.Proactive => CapabilityLevel.Authenticated,

            UserCapability.Admin => CapabilityLevel.Owner,
            _ => CapabilityLevel.Authenticated
        };
    }

    public bool CanAccess(UserCapability capability)
    {
        var required = GetRequiredLevel(capability);
        return required switch
        {
            CapabilityLevel.Public => true,
            CapabilityLevel.Authenticated => _currentUserService.IsAuthenticated,
            CapabilityLevel.Owner => _currentUserService.IsOwner,
            _ => false
        };
    }

    public string GetGatingTitle(UserCapability capability)
    {
        return capability switch
        {
            UserCapability.Memory => "Personal Memory Engine",
            UserCapability.Tasks => "Autonomous Task Execution",
            UserCapability.Automations => "Automations & Monitoring",
            UserCapability.PersistentHistory => "Conversation Archive",
            UserCapability.VoiceHistory => "Voice Timeline Sync",
            UserCapability.Files => "Filesystem & Workspace",
            UserCapability.Admin => "Owner Command Console",
            _ => "Personal AI Capabilities"
        };
    }

    public string GetGatingMessage(UserCapability capability)
    {
        return capability switch
        {
            UserCapability.Memory => "Sign in so SAVI can securely remember your preferences, facts, and personalized context across sessions.",
            UserCapability.Tasks => "Sign in to plan, run, and track multi-step agent tasks and workflows in your private workspace.",
            UserCapability.Automations => "Sign in to schedule recurring tasks, periodic monitors, and background automations.",
            UserCapability.PersistentHistory => "Sign in to securely save and revisit your previous conversations and voice transcripts.",
            UserCapability.VoiceHistory => "Sign in to preserve your full-duplex voice conversation timelines across devices.",
            UserCapability.Files => "Sign in to enable workspace filesystem operations and document inspection.",
            UserCapability.Admin => "This operational sector requires elevated Level 1 Owner clearance.",
            _ => "Sign in to unlock full personal AI assistant features with cross-device continuity."
        };
    }
}
