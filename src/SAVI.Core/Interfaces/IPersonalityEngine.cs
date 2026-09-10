using SAVI.Core.Entities;
using SAVI.Core.Enums;

namespace SAVI.Core.Interfaces;

public interface IPersonalityEngine
{
    string FormatResponse(
        string rawContent,
        PersonalityMode mode,
        IReadOnlyList<MemoryItem> relevantMemories,
        bool isVoice = false);

    string FormatFriendlyGreeting(string? userName = null);
    string FormatChitChat(string operation, string prompt, string? userName = null);
    string FormatError(string reason);
    string FormatActionApprovalPrompt(string actionDescription, PermissionLevel level);
}
