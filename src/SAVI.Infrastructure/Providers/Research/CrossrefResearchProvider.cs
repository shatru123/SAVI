using System.Text.Json;
using SAVI.Core.Constants;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Infrastructure.Providers.Research;

public class CrossrefResearchProvider : ICapabilityProvider
{
    private readonly HttpClient _httpClient;

    public CrossrefResearchProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Id => SaviConstants.Providers.Crossref;
    public string Name => "Crossref Academic Research & Papers (Free)";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.Research };
    public int Priority => 15;
    public ProviderCategory Category => ProviderCategory.SpecializedPublicApi;
    public ProviderCostType CostType => ProviderCostType.FreePublic;
    public double AuthorityLevel => 0.95;
    public double AccuracyScore => 0.95;
    public double ReliabilityScore => 0.95;
    public TimeSpan TypicalLatency => TimeSpan.FromMilliseconds(600);
    public TimeSpan Timeout => TimeSpan.FromSeconds(3);

    public bool CanHandle(TaskRequest request)
    {
        return request.Capability.Equals(SaviConstants.Capabilities.Research, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ProviderResult> ExecuteAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        var query = request.Parameters.GetValueOrDefault("query") ??
                    request.Parameters.GetValueOrDefault("topic") ??
                    request.Prompt;

        if (string.IsNullOrWhiteSpace(query))
        {
            return ProviderResult.Failed(Id, Name, "Query is empty.");
        }

        try
        {
            var url = $"https://api.crossref.org/works?query={Uri.EscapeDataString(query.Trim())}&rows=3";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("SAVI-Companion/1.0 (mailto:open-assistant@savi.local)");

            using var cts = new CancellationTokenSource(Timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
            using var resp = await _httpClient.SendAsync(req, linked.Token);

            if (!resp.IsSuccessStatusCode)
            {
                return ProviderResult.Failed(Id, Name, $"Crossref returned HTTP {(int)resp.StatusCode}");
            }

            var json = await resp.Content.ReadAsStringAsync(linked.Token);
            using var doc = JsonDocument.Parse(json);

            var messageObj = doc.RootElement.GetProperty("message");
            var items = messageObj.GetProperty("items");

            if (items.GetArrayLength() == 0)
            {
                return ProviderResult.Failed(Id, Name, $"No research publications found for \"{query}\".");
            }

            var sources = new List<SourceReference>();
            var papers = new List<object>();

            foreach (var item in items.EnumerateArray())
            {
                var title = item.TryGetProperty("title", out var tProp) && tProp.GetArrayLength() > 0
                    ? tProp[0].GetString() ?? "Untitled"
                    : "Untitled";

                var doi = item.TryGetProperty("DOI", out var doiProp) ? doiProp.GetString() ?? "" : "";
                var paperUrl = item.TryGetProperty("URL", out var urlProp) ? urlProp.GetString() ?? $"https://doi.org/{doi}" : $"https://doi.org/{doi}";
                var publisher = item.TryGetProperty("publisher", out var pubProp) ? pubProp.GetString() ?? "" : "";

                var authors = new List<string>();
                if (item.TryGetProperty("author", out var authArray))
                {
                    foreach (var a in authArray.EnumerateArray())
                    {
                        var family = a.TryGetProperty("family", out var fam) ? fam.GetString() ?? "" : "";
                        var given = a.TryGetProperty("given", out var giv) ? giv.GetString() ?? "" : "";
                        authors.Add($"{given} {family}".Trim());
                    }
                }

                var authorSummary = authors.Count > 0 ? string.Join(", ", authors.Take(3)) : "Various Authors";

                papers.Add(new
                {
                    Title = title,
                    DOI = doi,
                    Authors = authorSummary,
                    Publisher = publisher,
                    Url = paperUrl
                });

                sources.Add(new SourceReference
                {
                    Title = title,
                    Url = paperUrl,
                    SourceName = $"Crossref ({publisher})",
                    Snippet = $"{title} by {authorSummary}. DOI: {doi}",
                    ReliabilityScore = 0.95
                });
            }

            var first = papers[0];
            return ProviderResult.Succeeded(Id, Name, papers, confidence: 0.95, sources: sources);
        }
        catch (OperationCanceledException)
        {
            return ProviderResult.Failed(Id, Name, "Crossref request timed out.");
        }
        catch (Exception ex)
        {
            return ProviderResult.Failed(Id, Name, $"Crossref error: {ex.Message}");
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.crossref.org/works?rows=1");
            req.Headers.UserAgent.ParseAdd("SAVI-Companion/1.0 (mailto:open-assistant@savi.local)");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
            using var resp = await _httpClient.SendAsync(req, linked.Token);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
