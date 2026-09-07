using System.Text.Json;
using SAVI.Core.Constants;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Infrastructure.Providers.Search;

public class DuckDuckGoSearchProvider : ICapabilityProvider, ISearchProvider
{
    private readonly HttpClient _httpClient;

    public DuckDuckGoSearchProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SAVI-Companion/1.0 (Public Search Integration)");
    }

    public string Id => SaviConstants.Providers.DuckDuckGo;
    public string Name => "DuckDuckGo Instant Answer / Web Search (Free)";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.Search };
    public int Priority => 15;

    public bool CanHandle(TaskRequest request)
    {
        return request.Capability.Equals(SaviConstants.Capabilities.Search, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ProviderResult> ExecuteAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        var query = request.Parameters.GetValueOrDefault("query") ?? request.Prompt;
        var searchReq = new SearchRequest { Query = query, MaxResults = 5 };
        var result = await SearchAsync(searchReq, cancellationToken);

        if (result.Items.Count == 0)
        {
            return ProviderResult.Failed(Id, Name, $"No search results found for '{query}'.");
        }

        var sources = result.Items.Select(item => new SourceReference
        {
            Title = item.Title,
            Url = item.Url,
            Snippet = item.Snippet,
            SourceName = item.Source,
            ReliabilityScore = item.RelevanceScore,
            RetrievedAt = item.RetrievedAt
        }).ToList();

        return ProviderResult.Succeeded(Id, Name, result, confidence: 0.85, sources: sources);
    }

    public async Task<SearchResult> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        var items = new List<SearchItem>();

        try
        {
            var url = $"https://api.duckduckgo.com/?q={Uri.EscapeDataString(request.Query)}&format=json&no_html=1&skip_disambig=1";
            using var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var heading = root.TryGetProperty("Heading", out var hProp) ? hProp.GetString() : null;
                var abstractText = root.TryGetProperty("AbstractText", out var aProp) ? aProp.GetString() : null;
                var abstractUrl = root.TryGetProperty("AbstractURL", out var uProp) ? uProp.GetString() : null;
                var abstractSource = root.TryGetProperty("AbstractSource", out var sProp) ? sProp.GetString() : "DuckDuckGo";

                if (!string.IsNullOrWhiteSpace(abstractText))
                {
                    items.Add(new SearchItem
                    {
                        Title = string.IsNullOrWhiteSpace(heading) ? request.Query : heading,
                        Snippet = abstractText,
                        Url = string.IsNullOrWhiteSpace(abstractUrl) ? $"https://duckduckgo.com/?q={Uri.EscapeDataString(request.Query)}" : abstractUrl,
                        Source = abstractSource ?? "DuckDuckGo",
                        RelevanceScore = 0.95
                    });
                }

                // Check RelatedTopics
                if (root.TryGetProperty("RelatedTopics", out var relTopics) && relTopics.ValueKind == JsonValueKind.Array)
                {
                    foreach (var topic in relTopics.EnumerateArray())
                    {
                        if (items.Count >= request.MaxResults) break;

                        if (topic.TryGetProperty("Text", out var textProp) && topic.TryGetProperty("FirstURL", out var urlProp))
                        {
                            var text = textProp.GetString() ?? "";
                            var itemUrl = urlProp.GetString() ?? "";
                            if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(itemUrl))
                            {
                                items.Add(new SearchItem
                                {
                                    Title = text.Length > 50 ? text[..47] + "..." : text,
                                    Snippet = text,
                                    Url = itemUrl,
                                    Source = "DuckDuckGo Instant Answers",
                                    RelevanceScore = 0.85
                                });
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // Search provider gracefully returns whatever items were accumulated
        }

        return new SearchResult
        {
            Query = request.Query,
            TotalFound = items.Count,
            Items = items,
            SourceEngine = "DuckDuckGo"
        };
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var resp = await _httpClient.GetAsync("https://api.duckduckgo.com/?q=test&format=json", cancellationToken);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
