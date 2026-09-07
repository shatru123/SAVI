using SAVI.Core.Constants;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Infrastructure.Providers;

public class ProviderScorer
{
    public double WeightCapability { get; set; } = 0.25;
    public double WeightAccuracy { get; set; } = 0.20;
    public double WeightReliability { get; set; } = 0.15;
    public double WeightSpeed { get; set; } = 0.15;
    public double WeightFreshness { get; set; } = 0.10;
    public double WeightAvailability { get; set; } = 0.10;
    public double WeightZeroCost { get; set; } = 0.05;

    public ProviderScore Score(
        ICapabilityProvider provider,
        TaskRequest request,
        CircuitBreakerState circuitState = CircuitBreakerState.Healthy,
        bool isRateLimited = false)
    {
        if (!provider.CanHandle(request))
        {
            return new ProviderScore
            {
                ProviderId = provider.Id,
                ProviderName = provider.Name,
                TotalScore = 0.0
            };
        }

        // 1. Capability Match (1.0 for primary, 0.5 for general fallback)
        double capabilityMatch = 0.5;
        if (provider.Capabilities.Contains(request.Capability, StringComparer.OrdinalIgnoreCase))
        {
            capabilityMatch = 1.0;
        }

        // Deterministic tasks must NEVER prefer generic AI reasoning providers
        bool isDeterministicTask = request.Capability.Equals(SaviConstants.Capabilities.Calculator, StringComparison.OrdinalIgnoreCase) ||
                                   request.Capability.Equals(SaviConstants.Capabilities.System, StringComparison.OrdinalIgnoreCase) ||
                                   request.Capability.Equals(SaviConstants.Capabilities.Time, StringComparison.OrdinalIgnoreCase) ||
                                   request.Capability.Equals(SaviConstants.Capabilities.Currency, StringComparison.OrdinalIgnoreCase) ||
                                   request.Capability.Equals(SaviConstants.Capabilities.Weather, StringComparison.OrdinalIgnoreCase);

        if (isDeterministicTask && provider.Category == ProviderCategory.ReasoningSynthesis)
        {
            capabilityMatch = 0.05;
        }

        // 2. Accuracy
        double accuracy = provider.AccuracyScore;

        // 3. Reliability
        double reliability = provider.ReliabilityScore;

        // 4. Speed Score (Higher is faster)
        var latencyMs = provider.TypicalLatency.TotalMilliseconds;
        double speed = latencyMs switch
        {
            <= 100 => 1.0,
            <= 500 => 0.9,
            <= 1500 => 0.75,
            <= 3000 => 0.5,
            <= 6000 => 0.3,
            _ => 0.1
        };

        // 5. Freshness
        double freshness = provider.Category switch
        {
            ProviderCategory.LocalDeterministic => 1.0,
            ProviderCategory.SpecializedPublicApi => 0.95,
            ProviderCategory.WebSearch => 0.90,
            ProviderCategory.KnowledgeBase => 0.85,
            ProviderCategory.ReasoningSynthesis => 0.70,
            _ => 0.80
        };

        // 6. Availability based on circuit state
        double availability = circuitState switch
        {
            CircuitBreakerState.Healthy => 1.0,
            CircuitBreakerState.HalfOpen => 0.5,
            CircuitBreakerState.Degraded => 0.3,
            CircuitBreakerState.Open => 0.0,
            _ => 0.5
        };

        // 7. Zero-Cost preference
        double zeroCost = provider.CostType switch
        {
            ProviderCostType.LocalZeroCost => 1.0,
            ProviderCostType.FreePublic => 0.95,
            ProviderCostType.OptionalKey => 0.5,
            ProviderCostType.OptionalPaid => 0.1,
            _ => 0.5
        };

        // Penalties
        double penalty = 0.0;
        if (circuitState == CircuitBreakerState.Open)
        {
            penalty += 1.0;
        }
        else if (isRateLimited)
        {
            penalty += 0.40;
        }

        return ProviderScore.Compute(
            provider.Id,
            provider.Name,
            capabilityMatch,
            accuracy,
            reliability,
            speed,
            freshness,
            availability,
            zeroCost,
            penalty,
            WeightCapability,
            WeightAccuracy,
            WeightReliability,
            WeightSpeed,
            WeightFreshness,
            WeightAvailability,
            WeightZeroCost);
    }
}
