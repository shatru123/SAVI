using System.Text.Json;
using SAVI.Core.Constants;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Infrastructure.Providers.Knowledge;

public class WikipediaKnowledgeProvider : ICapabilityProvider
{
    private readonly HttpClient _httpClient;

    public WikipediaKnowledgeProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SAVI-Companion/1.0 (https://github.com/shatru123/SAVI; contact: shatru@savi.local)");
    }

    public string Id => SaviConstants.Providers.Wikipedia;
    public string Name => "Wikipedia Open Knowledge (Free)";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.Knowledge };
    public int Priority => 10;

    public bool CanHandle(TaskRequest request)
    {
        return request.Capability.Equals(SaviConstants.Capabilities.Knowledge, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ProviderResult> ExecuteAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        var topic = request.Parameters.GetValueOrDefault("topic") ??
                    request.Parameters.GetValueOrDefault("query") ?? request.Prompt;

        try
        {
            // First try summary API
            var summaryUrl = $"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(topic)}";
            using var response = await _httpClient.GetAsync(summaryUrl, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var title = root.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? topic : topic;
                var extract = root.TryGetProperty("extract", out var eProp) ? eProp.GetString() ?? "" : "";
                var pageUrl = root.TryGetProperty("content_urls", out var cuProp) &&
                              cuProp.TryGetProperty("desktop", out var dProp) &&
                              dProp.TryGetProperty("page", out var pProp)
                    ? pProp.GetString() ?? $"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(title)}"
                    : $"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(title)}";

                var source = new SourceReference
                {
                    Title = $"{title} — Wikipedia",
                    Url = pageUrl,
                    SourceName = "Wikipedia",
                    Snippet = extract,
                    ReliabilityScore = 0.92
                };

                var data = new
                {
                    Title = title,
                    Summary = extract,
                    Url = pageUrl
                };

                return ProviderResult.Succeeded(Id, Name, data, confidence: 0.92, sources: new[] { source });
            }

            // Fallback to Wikipedia search API
            var searchUrl = $"https://en.wikipedia.org/w/api.php?action=opensearch&search={Uri.EscapeDataString(topic)}&limit=3&namespace=0&format=json";
            using var searchResp = await _httpClient.GetAsync(searchUrl, cancellationToken);
            if (searchResp.IsSuccessStatusCode)
            {
                var sJson = await searchResp.Content.ReadAsStringAsync(cancellationToken);
                using var sDoc = JsonDocument.Parse(sJson);
                var titles = sDoc.RootElement[1];
                var descs = sDoc.RootElement[2];
                var links = sDoc.RootElement[3];

                if (titles.GetArrayLength() > 0)
                {
                    var firstTitle = titles[0].GetString() ?? topic;
                    var firstDesc = descs[0].GetString() ?? "";
                    var firstLink = links[0].GetString() ?? "";

                    var source = new SourceReference
                    {
                        Title = firstTitle,
                        Url = firstLink,
                        SourceName = "Wikipedia",
                        Snippet = firstDesc,
                        ReliabilityScore = 0.88
                    };

                    var data = new
                    {
                        Title = firstTitle,
                        Summary = firstDesc,
                        Url = firstLink
                    };

                    return ProviderResult.Succeeded(Id, Name, data, confidence: 0.88, sources: new[] { source });
                }
            }

            return ProviderResult.Failed(Id, Name, $"No knowledge found on Wikipedia for '{topic}'.");
        }
        catch (Exception ex)
        {
            return ProviderResult.Failed(Id, Name, $"Wikipedia search failed: {ex.Message}");
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var resp = await _httpClient.GetAsync("https://en.wikipedia.org/api/rest_v1/page/summary/Earth", cancellationToken);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
