using System.Text.Json;
using SAVI.Core.Constants;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Infrastructure.Providers.GitHub;

public class GitHubPublicProvider : ICapabilityProvider
{
    private readonly HttpClient _httpClient;

    public GitHubPublicProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SAVI-Companion/1.0");
    }

    public string Id => SaviConstants.Providers.GitHubPublic;
    public string Name => "GitHub Public REST API (Free)";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.GitHub };
    public int Priority => 10;
    public SAVI.Core.Enums.ProviderCategory Category => SAVI.Core.Enums.ProviderCategory.SpecializedPublicApi;
    public SAVI.Core.Enums.ProviderCostType CostType => SAVI.Core.Enums.ProviderCostType.FreePublic;
    public double AuthorityLevel => 0.95;
    public double AccuracyScore => 0.95;
    public double ReliabilityScore => 0.90;
    public TimeSpan TypicalLatency => TimeSpan.FromMilliseconds(500);
    public TimeSpan Timeout => TimeSpan.FromSeconds(3);

    public bool CanHandle(TaskRequest request)
    {
        return request.Capability.Equals(SaviConstants.Capabilities.GitHub, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ProviderResult> ExecuteAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        var repo = request.Parameters.GetValueOrDefault("repo") ?? "dotnet/runtime";
        var user = request.Parameters.GetValueOrDefault("user");

        try
        {
            if (!string.IsNullOrWhiteSpace(repo))
            {
                var cleanRepo = repo.Trim().Trim('/');
                if (!cleanRepo.Contains('/')) cleanRepo = $"shatru123/{cleanRepo}";

                var url = $"https://api.github.com/repos/{cleanRepo}";
                using var response = await _httpClient.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return ProviderResult.Failed(Id, Name, $"GitHub repository query for '{cleanRepo}' returned {response.StatusCode}");
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var fullName = root.GetProperty("full_name").GetString() ?? cleanRepo;
                var description = root.TryGetProperty("description", out var dProp) ? dProp.GetString() ?? "" : "";
                var stars = root.TryGetProperty("stargazers_count", out var sProp) ? sProp.GetInt32() : 0;
                var forks = root.TryGetProperty("forks_count", out var fProp) ? fProp.GetInt32() : 0;
                var issues = root.TryGetProperty("open_issues_count", out var iProp) ? iProp.GetInt32() : 0;
                var htmlUrl = root.GetProperty("html_url").GetString() ?? $"https://github.com/{cleanRepo}";
                var defaultBranch = root.TryGetProperty("default_branch", out var bProp) ? bProp.GetString() ?? "main" : "main";

                var data = new
                {
                    Repository = fullName,
                    Description = description,
                    Stars = stars,
                    Forks = forks,
                    OpenIssues = issues,
                    DefaultBranch = defaultBranch,
                    Url = htmlUrl
                };

                var source = new SourceReference
                {
                    Title = $"{fullName} on GitHub",
                    Url = htmlUrl,
                    SourceName = "GitHub",
                    Snippet = $"{description} | Stars: {stars:N0} | Forks: {forks:N0} | Open issues: {issues:N0}",
                    ReliabilityScore = 0.98
                };

                return ProviderResult.Succeeded(Id, Name, data, confidence: 0.98, sources: new[] { source });
            }

            return ProviderResult.Failed(Id, Name, "No repo or user parameter supplied for GitHub query.");
        }
        catch (Exception ex)
        {
            return ProviderResult.Failed(Id, Name, $"GitHub API call failed: {ex.Message}");
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var resp = await _httpClient.GetAsync("https://api.github.com/repos/dotnet/core", cancellationToken);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
