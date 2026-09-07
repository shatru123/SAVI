using System.Text.RegularExpressions;

namespace SAVI.Infrastructure.Security;

public static class SecretMasker
{
    private static readonly Regex BearerRegex = new(@"(Bearer\s+)[A-Za-z0-9\-\._~\+\/]+=*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ApiKeyRegex = new(@"((?:api_?key|api_?token|secret|password|access_?token)\s*[:=]\s*[""']?)[^""'\s&]+([""']?)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Mask(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        var masked = BearerRegex.Replace(input, "$1[REDACTED]");
        masked = ApiKeyRegex.Replace(masked, "$1[REDACTED]$2");
        return masked;
    }
}
