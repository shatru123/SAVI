using SAVI.Agent.Routing;
using SAVI.Core.Constants;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;

namespace SAVI.Agent.Planning;

public record ExecutionPlan(
    string Capability,
    IReadOnlyList<ICapabilityProvider> PrimaryProviders,
    IReadOnlyList<ICapabilityProvider> VerificationProviders,
    bool RequiresTool,
    string? ToolName,
    ToolInput? ToolInput,
    VerificationPolicy Policy = VerificationPolicy.Balanced);

public class ExecutionPlanner
{
    private readonly IProviderRegistry _providerRegistry;

    public ExecutionPlanner(IProviderRegistry providerRegistry)
    {
        _providerRegistry = providerRegistry;
    }

    public ExecutionPlan CreatePlan(DetectedIntent intent, AgentRequest request, VerificationPolicy defaultPolicy = VerificationPolicy.Balanced)
    {
        var policy = request.VerificationPolicyOverride ?? intent.PolicyOverride ?? defaultPolicy;

        // 1. FileSystem or Terminal tools
        if (intent.Capability == SaviConstants.Capabilities.FileSystem)
        {
            var action = intent.Operation;
            var perm = action == "delete" ? PermissionLevel.Dangerous : PermissionLevel.Controlled;
            var toolInput = new ToolInput
            {
                ToolName = "file_system",
                Action = action,
                Arguments = intent.Parameters,
                RequiredPermission = perm,
                UserConfirmed = request.ActionApproved
            };

            return new ExecutionPlan(intent.Capability, Array.Empty<ICapabilityProvider>(), Array.Empty<ICapabilityProvider>(), true, "file_system", toolInput, policy);
        }

        if (intent.Capability == SaviConstants.Capabilities.Terminal)
        {
            var toolInput = new ToolInput
            {
                ToolName = "terminal",
                Action = "execute",
                Arguments = intent.Parameters,
                RequiredPermission = PermissionLevel.Dangerous,
                UserConfirmed = request.ActionApproved
            };

            return new ExecutionPlan(intent.Capability, Array.Empty<ICapabilityProvider>(), Array.Empty<ICapabilityProvider>(), true, "terminal", toolInput, policy);
        }

        // 2. Discover capability providers using dynamic scoring
        var taskReq = new TaskRequest
        {
            Capability = intent.Capability,
            Operation = intent.Operation,
            Parameters = intent.Parameters,
            Prompt = request.Message,
            ConversationId = request.ConversationId
        };

        var matched = _providerRegistry.RankProviders(taskReq);

        IReadOnlyList<ICapabilityProvider> primary;
        IReadOnlyList<ICapabilityProvider> verification;

        switch (policy)
        {
            case VerificationPolicy.Fast:
                // Best provider only, zero verification overhead
                primary = matched.Take(1).ToList();
                verification = Array.Empty<ICapabilityProvider>();
                break;

            case VerificationPolicy.Verified:
                // Multiple independent providers concurrently for consensus
                primary = matched.Take(2).ToList();
                verification = matched.Skip(2).Take(1).ToList();
                break;

            case VerificationPolicy.Balanced:
            default:
                // Best provider primary, fallback/verification standby
                primary = matched.Take(1).ToList();
                verification = matched.Skip(1).Take(1).ToList();
                break;
        }

        return new ExecutionPlan(intent.Capability, primary, verification, false, null, null, policy);
    }
}
