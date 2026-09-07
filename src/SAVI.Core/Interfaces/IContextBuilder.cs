using SAVI.Core.Models;

namespace SAVI.Core.Interfaces;

public interface IContextBuilder
{
    Task<ContextPackage> BuildContextAsync(AgentRequest request, CancellationToken cancellationToken = default);
}
