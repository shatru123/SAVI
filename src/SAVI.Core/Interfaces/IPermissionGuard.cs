using SAVI.Core.Enums;
using SAVI.Core.Models;

namespace SAVI.Core.Interfaces;

public interface IPermissionGuard
{
    Task<bool> CanExecuteAsync(ToolInput input, CancellationToken cancellationToken = default);
    ActionApprovalRequest CreateApprovalRequest(ToolInput input);
}
