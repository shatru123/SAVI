namespace SAVI.Core.ValueObjects;

public sealed record ProviderScore
{
    public string ProviderId { get; init; } = string.Empty;
    public string ProviderName { get; init; } = string.Empty;
    public double CapabilityMatch { get; init; }
    public double Accuracy { get; init; }
    public double Reliability { get; init; }
    public double Speed { get; init; }
    public double Freshness { get; init; }
    public double Availability { get; init; }
    public double ZeroCost { get; init; }
    public double Penalty { get; init; }
    public double TotalScore { get; init; }

    public static ProviderScore Compute(
        string providerId,
        string providerName,
        double capabilityMatch,
        double accuracy,
        double reliability,
        double speed,
        double freshness,
        double availability,
        double zeroCost,
        double penalty = 0.0,
        double wCapability = 0.25,
        double wAccuracy = 0.20,
        double wReliability = 0.15,
        double wSpeed = 0.15,
        double wFreshness = 0.10,
        double wAvailability = 0.10,
        double wZeroCost = 0.05)
    {
        var rawScore = (capabilityMatch * wCapability)
                     + (accuracy * wAccuracy)
                     + (reliability * wReliability)
                     + (speed * wSpeed)
                     + (freshness * wFreshness)
                     + (availability * wAvailability)
                     + (zeroCost * wZeroCost)
                     - penalty;

        var total = Math.Clamp(rawScore, 0.0, 1.0);

        return new ProviderScore
        {
            ProviderId = providerId,
            ProviderName = providerName,
            CapabilityMatch = capabilityMatch,
            Accuracy = accuracy,
            Reliability = reliability,
            Speed = speed,
            Freshness = freshness,
            Availability = availability,
            ZeroCost = zeroCost,
            Penalty = penalty,
            TotalScore = total
        };
    }
}
