using SAVI.Infrastructure.Security;
using Xunit;

namespace SAVI.Infrastructure.Tests;

public class SecurityTests
{
    [Theory]
    [InlineData("http://localhost:8080/api")]
    [InlineData("http://127.0.0.1/admin")]
    [InlineData("https://10.0.0.1/internal")]
    [InlineData("http://192.168.1.1/router")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("ftp://example.com")]
    public void SsrfValidator_ShouldBlockUnsafeEndpoints(string url)
    {
        var isSafe = SsrfValidator.IsUrlSafe(url, out var reason);
        Assert.False(isSafe);
        Assert.NotNull(reason);
    }

    [Theory]
    [InlineData("https://api.open-meteo.com/v1/forecast")]
    [InlineData("https://api.frankfurter.app/latest")]
    [InlineData("https://en.wikipedia.org/api/rest_v1/page/summary/Dotnet")]
    public void SsrfValidator_ShouldAllowLegitimatePublicUrls(string url)
    {
        var isSafe = SsrfValidator.IsUrlSafe(url, out var reason);
        Assert.True(isSafe);
        Assert.Null(reason);
    }

    [Fact]
    public void SecretMasker_ShouldMaskBearerTokensAndApiKeys()
    {
        var input = "Authorization: Bearer mySecretToken123456 and api_key=xyz987654";
        var masked = SecretMasker.Mask(input);

        Assert.DoesNotContain("mySecretToken123456", masked);
        Assert.DoesNotContain("xyz987654", masked);
        Assert.Contains("[REDACTED]", masked);
    }
}
