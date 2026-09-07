using SAVI.Core.Enums;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Core.Interfaces;

public interface ITool
{
    string Name { get; }
    string Description { get; }
    PermissionLevel PermissionLevel { get; }
    Task<ToolExecutionResult> ExecuteAsync(ToolInput input, CancellationToken cancellationToken = default);
}
