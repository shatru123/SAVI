using SAVI.Core.Enums;

namespace SAVI.Core.ValueObjects;

public sealed record ToolMetadata
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public PermissionLevel PermissionLevel { get; init; } = PermissionLevel.Safe;
    public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();
}
