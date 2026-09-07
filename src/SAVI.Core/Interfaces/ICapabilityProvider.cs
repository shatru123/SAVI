using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Core.Interfaces;

public interface ICapabilityProvider
{
    string Id { get; }
    string Name { get; }
    IReadOnlyCollection<string> Capabilities { get; }
    int Priority { get; }
    bool CanHandle(TaskRequest request);
    Task<ProviderResult> ExecuteAsync(TaskRequest request, CancellationToken cancellationToken = default);
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}
