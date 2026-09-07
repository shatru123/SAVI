using SAVI.Agent.Routing;
using SAVI.Core.Constants;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;

namespace SAVI.Agent.Planning;

public record ExecutionPlan(
    string Capability,
    IReadOnlyList<ICapabilityProvider> PrimaryProviders,
    IReadOnlyList<ICapabilityProvider> VerificationProviders,
    bool RequiresTool,
    string? ToolName,
    ToolInput? ToolInput);

public class ExecutionPlanner
{
    private readonly IProviderRegistry _providerRegistry;

    public ExecutionPlanner(IProviderRegistry providerRegistry)
    {
        _providerRegistry = providerRegistry;
    }

    public ExecutionPlan CreatePlan(DetectedIntent intent, AgentRequest request)
    {
        // 1. FileSystem or Terminal tools
        if (intent.Capability == SaviConstants.Capabilities.FileSystem)
        {
            var action = intent.Operation;
            var perm = action == "delete" ? Core.Enums.PermissionLevel.Dangerous : Core.Enums.PermissionLevel.Controlled;
            var toolInput = new ToolInput
            {
                ToolName = "file_system",
                Action = action,
                Arguments = intent.Parameters,
                RequiredPermission = perm,
                UserConfirmed = request.ActionApproved
            };

            return new ExecutionPlan(intent.Capability, Array.Empty<ICapabilityProvider>(), Array.Empty<ICapabilityProvider>(), true, "file_system", toolInput);
        }

        if (intent.Capability == SaviConstants.Capabilities.Terminal)
        {
            var toolInput = new ToolInput
            {
                ToolName = "terminal",
                Action = "execute",
                Arguments = intent.Parameters,
                RequiredPermission = Core.Enums.PermissionLevel.Dangerous,
                UserConfirmed = request.ActionApproved
            };

            return new ExecutionPlan(intent.Capability, Array.Empty<ICapabilityProvider>(), Array.Empty<ICapabilityProvider>(), true, "terminal", toolInput);
        }

        // 2. Discover capability providers
        var taskReq = new TaskRequest
        {
            Capability = intent.Capability,
            Operation = intent.Operation,
            Parameters = intent.Parameters,
            Prompt = request.Message,
            ConversationId = request.ConversationId
        };

        var matched = _providerRegistry.RankProviders(taskReq);
        var primary = matched.Take(1).ToList();
        var verification = matched.Skip(1).Take(2).ToList(); // Fallback & verification providers

        return new ExecutionPlan(intent.Capability, primary, verification, false, null, null);
    }
}
