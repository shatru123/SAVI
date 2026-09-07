using SAVI.Core.Entities;

namespace SAVI.Core.Models;

public sealed record TaskRequest
{
    public string Prompt { get; init; } = string.Empty;
    public string Capability { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty;
    public Dictionary<string, string> Parameters { get; init; } = new();
    public string ConversationId { get; init; } = string.Empty;
    public ContextPackage? Context { get; init; }
}
