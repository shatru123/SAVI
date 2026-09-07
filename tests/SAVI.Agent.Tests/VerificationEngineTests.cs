using SAVI.Agent.Verification;
using SAVI.Core.ValueObjects;
using Xunit;

namespace SAVI.Agent.Tests;

public class VerificationEngineTests
{
    private readonly VerificationEngine _engine = new();

    [Fact]
    public async Task Verify_SingleSuccessfulProvider_ShouldReturnVerifiedWithHighConfidence()
    {
        var src = new SourceReference { SourceName = "Open-Meteo", Title = "Weather" };
        var results = new[]
        {
            ProviderResult.Succeeded("p1", "Open-Meteo", new { TemperatureCelsius = 22.0 }, 0.95, new[] { src })
        };

        var verification = await _engine.VerifyAndCompareAsync("Weather in Berlin", results);

        Assert.True(verification.IsVerified);
        Assert.False(verification.HasContradictions);
        Assert.Equal(0.95, verification.Confidence);
        Assert.Single(verification.Sources);
    }

    [Fact]
    public async Task Verify_ConflictingProviders_ShouldDetectContradictionAndExplain()
    {
        var src1 = new SourceReference { SourceName = "Open-Meteo", Title = "Weather" };
        var src2 = new SourceReference { SourceName = "wttr.in", Title = "Weather" };

        var results = new[]
        {
            ProviderResult.Succeeded("p1", "Open-Meteo", new { TemperatureCelsius = 15.0 }, 0.95, new[] { src1 }),
            ProviderResult.Succeeded("p2", "wttr.in", new { TemperatureCelsius = 28.0 }, 0.90, new[] { src2 })
        };

        var verification = await _engine.VerifyAndCompareAsync("Weather in London", results);

        Assert.True(verification.HasContradictions);
        Assert.NotNull(verification.ContradictionExplanation);
        Assert.Contains("Discrepancy detected", verification.ContradictionExplanation);
    }
}
