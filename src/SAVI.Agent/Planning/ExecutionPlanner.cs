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

    public ExecutionPlan CreatePlan(
        DetectedIntent intent,
        AgentRequest request,
        VerificationPolicy defaultPolicy = VerificationPolicy.Balanced,
        QueryAnalysisResult? analysis = null)
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
            ConversationId = request.ConversationId,
            Analysis = analysis
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

    public void RecordProviderResult(string providerId, long latencyMs, bool succeeded, bool rateLimited = false, TimeSpan? retryAfter = null)
    {
        _providerRegistry.RecordResult(providerId, latencyMs, succeeded, rateLimited, retryAfter);
    }

    public AgentPlan CreateAgentPlan(AgentRequest request, DetectedIntent intent, QueryAnalysisResult? analysis = null)
    {
        var prompt = request.Message.Trim();
        var lower = prompt.ToLowerInvariant();
        var requestType = ClassifyRequestType(lower, intent);
        var steps = new List<PlanStep>();

        // Multi-Step Comparative or Research Task Decomposition
        if (lower.Contains("compare") || lower.Contains("vs") || lower.Contains("difference between"))
        {
            steps.Add(new PlanStep
            {
                Order = 1,
                Title = "Identify Entities & Criteria",
                Description = "Extract targets to compare from prompt",
                Capability = intent.Capability,
                SkillOrToolName = "search"
            });
            steps.Add(new PlanStep
            {
                Order = 2,
                Title = "Gather Domain Evidence",
                Description = "Retrieve specifications and real-time facts for each target",
                Capability = intent.Capability,
                SkillOrToolName = intent.Capability == "weather" ? "weather" : "search",
                Dependencies = new[] { steps[0].Id }
            });
            steps.Add(new PlanStep
            {
                Order = 3,
                Title = "Synthesize Comparative Analysis",
                Description = "Evaluate trade-offs, metrics, and formulate response",
                Capability = "synthesis",
                SkillOrToolName = "synthesis",
                Dependencies = new[] { steps[1].Id }
            });
        }
        else if (requestType == "Research")
        {
            steps.Add(new PlanStep
            {
                Order = 1,
                Title = "Primary Information Retrieval",
                Description = "Query authoritative knowledge bases and web sources",
                Capability = intent.Capability,
                SkillOrToolName = "wikipedia"
            });
            steps.Add(new PlanStep
            {
                Order = 2,
                Title = "Cross-Verification Search",
                Description = "Gather supplementary evidence for consensus check",
                Capability = "web_search",
                SkillOrToolName = "search"
            });
            steps.Add(new PlanStep
            {
                Order = 3,
                Title = "Final Evidence Synthesis",
                Description = "Assemble coherent verified answer",
                Capability = "synthesis",
                SkillOrToolName = "synthesis",
                Dependencies = new[] { steps[0].Id, steps[1].Id }
            });
        }
        else
        {
            // Direct Single-Stage Execution
            steps.Add(new PlanStep
            {
                Order = 1,
                Title = $"Execute {intent.Capability}",
                Description = $"Process user request via {intent.Capability} capability",
                Capability = intent.Capability,
                SkillOrToolName = intent.Capability,
                Arguments = intent.Parameters,
                RequiresConfirmation = intent.Capability == SaviConstants.Capabilities.Terminal ||
                                       (intent.Capability == SaviConstants.Capabilities.FileSystem && intent.Operation == "delete")
            });
        }

        return new AgentPlan
        {
            Goal = prompt,
            RequestType = requestType,
            Steps = steps,
            Status = PlanExecutionStatus.Pending,
            VerificationPolicy = request.VerificationPolicyOverride ?? intent.PolicyOverride ?? VerificationPolicy.Balanced
        };
    }

    private static string ClassifyRequestType(string lower, DetectedIntent intent)
    {
        if (intent.Capability == "chitchat" || lower.StartsWith("hi") || lower.StartsWith("hello") || lower.StartsWith("hey"))
            return "Conversation";
        if (lower.Contains("compare") || lower.Contains("difference") || lower.Contains("analyze"))
            return "Analysis";
        if (lower.Contains("research") || lower.Contains("tell me about") || lower.Contains("explain"))
            return "Research";
        if (lower.Contains("remind") || lower.Contains("every") || lower.Contains("schedule") || lower.Contains("monitor"))
            return "Automation";
        if (lower.StartsWith("create") || lower.StartsWith("write") || lower.StartsWith("build"))
            return "Creation";
        if (lower.StartsWith("delete") || lower.StartsWith("run") || lower.StartsWith("execute") || lower.StartsWith("set"))
            return "Command";
        if (intent.Capability == "weather" || intent.Capability == "calculator" || intent.Capability == "currency")
            return "Question";

        return "Task";
    }
}
