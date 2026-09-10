using Microsoft.Extensions.DependencyInjection;
using SAVI.Agent.Context;
using SAVI.Agent.Memory;
using SAVI.Agent.Orchestration;
using SAVI.Agent.Personality;
using SAVI.Agent.Planning;
using SAVI.Agent.Routing;
using SAVI.Agent.Verification;
using SAVI.Core.Interfaces;

using SAVI.Agent.Understanding;
using SAVI.Agent.Synthesis;
using SAVI.Agent.Knowledge;

namespace SAVI.Agent;

public static class DependencyInjection
{
    public static IServiceCollection AddSaviAgent(this IServiceCollection services)
    {
        services.AddSingleton<ISaviSelfKnowledgeService, SaviSelfKnowledgeService>();
        services.AddSingleton<IQueryUnderstandingService, QueryUnderstandingService>();
        services.AddSingleton<EvidenceAggregator>();
        services.AddSingleton<IAnswerSynthesisService, AnswerSynthesisService>();
        services.AddScoped<IContextBuilder, ContextBuilder>();
        services.AddSingleton<IntentDetector>();
        services.AddScoped<ExecutionPlanner>();
        services.AddSingleton<IVerificationEngine, VerificationEngine>();
        services.AddSingleton<IPersonalityEngine, PersonalityEngine>();
        services.AddScoped<IMemoryExtractor, MemoryExtractor>();
        services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();

        return services;
    }
}
