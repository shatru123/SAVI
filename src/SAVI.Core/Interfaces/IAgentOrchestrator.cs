using SAVI.Core.Models;

namespace SAVI.Core.Interfaces;

public interface IAgentOrchestrator
{
    Task<AgentResponse> ProcessAsync(AgentRequest request, CancellationToken cancellationToken = default);
}
