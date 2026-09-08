using System.Text.Json;
using System.Text.RegularExpressions;
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
    }

    public string Id => SaviConstants.Providers.Wikipedia;
    public string Name => "Wikipedia Open Knowledge (Free)";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.Knowledge };
    public int Priority => 20;
    public SAVI.Core.Enums.ProviderCategory Category => SAVI.Core.Enums.ProviderCategory.KnowledgeBase;
    public SAVI.Core.Enums.ProviderCostType CostType => SAVI.Core.Enums.ProviderCostType.FreePublic;
    public double AuthorityLevel => 0.85;
    public double AccuracyScore => 0.85;
    public double ReliabilityScore => 0.95;
    public TimeSpan TypicalLatency => TimeSpan.FromMilliseconds(500);
    public TimeSpan Timeout => TimeSpan.FromSeconds(3);

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
            var slug = ResolveWikipediaSlug(topic);

            // First try summary API with resolved slug
            var summaryUrl = $"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(slug)}";
            using var req = new HttpRequestMessage(HttpMethod.Get, summaryUrl);
            req.Headers.TryAddWithoutValidation("User-Agent", "SAVI-Companion/1.0 (contact: info@savi.ai)");
            using var response = await _httpClient.SendAsync(req, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var title = root.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? topic : topic;
                var extract = root.TryGetProperty("extract", out var eProp) ? eProp.GetString() ?? "" : "";
                var desc = root.TryGetProperty("description", out var dProp) ? dProp.GetString() ?? "" : "";

                if (!string.IsNullOrWhiteSpace(extract) && IsCandidateRelevant(topic, title, extract))
                {
                    var pageUrl = root.TryGetProperty("content_urls", out var cuProp) &&
                                  cuProp.TryGetProperty("desktop", out var desktopProp) &&
                                  desktopProp.TryGetProperty("page", out var pProp)
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
                        Description = desc,
                        Url = pageUrl
                    };

                    return ProviderResult.Succeeded(Id, Name, data, confidence: 0.92, sources: new[] { source });
                }
            }

            // Fallback: Rich query search API
            var searchQuery = topic;

            var searchUrl = $"https://en.wikipedia.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(searchQuery)}&format=json&utf8=";
            using var sReq = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            sReq.Headers.TryAddWithoutValidation("User-Agent", "SAVI-Companion/1.0 (contact: info@savi.ai)");
            using var searchResp = await _httpClient.SendAsync(sReq, cancellationToken);
            if (searchResp.IsSuccessStatusCode)
            {
                var sJson = await searchResp.Content.ReadAsStringAsync(cancellationToken);
                using var sDoc = JsonDocument.Parse(sJson);
                if (sDoc.RootElement.TryGetProperty("query", out var qObj) &&
                    qObj.TryGetProperty("search", out var searchArr) &&
                    searchArr.GetArrayLength() > 0)
                {
                    var candidates = searchArr.EnumerateArray()
                        .Select(item => new
                        {
                            Item = item,
                            Title = item.GetProperty("title").GetString() ?? topic,
                            Snippet = Regex.Replace(item.GetProperty("snippet").GetString() ?? "", "<.*?>", string.Empty)
                        })
                        .OrderByDescending(candidate => CandidateScore(topic, candidate.Title, candidate.Snippet))
                        .ToList();
                    var topMatch = candidates.FirstOrDefault(candidate => IsCandidateRelevant(topic, candidate.Title, candidate.Snippet));
                    if (topMatch == null) return ProviderResult.Failed(Id, Name, $"No relevant knowledge found on Wikipedia for '{topic}'.");
                    var topTitle = topMatch.Title;
                    var rawSnippet = topMatch.Snippet;
                    var cleanSnippet = rawSnippet;
                    var articleUrl = $"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(topTitle.Replace(' ', '_'))}";

                    // Try to fetch the full summary of this top matched article
                    try
                    {
                        var topSummaryUrl = $"https://en.wikipedia.org/api/rest_v1/page/summary/{Uri.EscapeDataString(topTitle.Replace(' ', '_'))}";
                        using var topReq = new HttpRequestMessage(HttpMethod.Get, topSummaryUrl);
                        topReq.Headers.TryAddWithoutValidation("User-Agent", "SAVI-Companion/1.0 (contact: info@savi.ai)");
                        using var topResp = await _httpClient.SendAsync(topReq, cancellationToken);
                        if (topResp.IsSuccessStatusCode)
                        {
                            var topJson = await topResp.Content.ReadAsStringAsync(cancellationToken);
                            using var topDoc = JsonDocument.Parse(topJson);
                            var topExtract = topDoc.RootElement.TryGetProperty("extract", out var teProp) ? teProp.GetString() ?? cleanSnippet : cleanSnippet;

                            var topSource = new SourceReference
                            {
                                Title = $"{topTitle} — Wikipedia",
                                Url = articleUrl,
                                SourceName = "Wikipedia",
                                Snippet = topExtract,
                                ReliabilityScore = 0.90
                            };

                            var topData = new
                            {
                                Title = topTitle,
                                Summary = topExtract,
                                Url = articleUrl
                            };

                            return ProviderResult.Succeeded(Id, Name, topData, confidence: 0.90, sources: new[] { topSource });
                        }
                    }
                    catch { }

                    var source = new SourceReference
                    {
                        Title = $"{topTitle} — Wikipedia",
                        Url = articleUrl,
                        SourceName = "Wikipedia",
                        Snippet = cleanSnippet,
                        ReliabilityScore = 0.85
                    };

                    var data = new
                    {
                        Title = topTitle,
                        Summary = cleanSnippet,
                        Url = articleUrl
                    };

                    return ProviderResult.Succeeded(Id, Name, data, confidence: 0.85, sources: new[] { source });
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

    private static string ResolveWikipediaSlug(string topic)
    {
        return topic.Trim().Replace(' ', '_');
    }

    private static bool IsCandidateRelevant(string query, string title, string content)
    {
        return CandidateScore(query, title, content) >= 0.35;
    }

    private static double CandidateScore(string query, string title, string content)
    {
        var queryTerms = Terms(query);
        if (queryTerms.Count == 0) return 0;
        var titleTerms = Terms(title);
        var contentTerms = Terms(content);
        var titleMatches = queryTerms.Count(titleTerms.Contains);
        var contentMatches = queryTerms.Count(contentTerms.Contains);
        return Math.Clamp((titleMatches / (double)queryTerms.Count) * 0.75 +
                          (contentMatches / (double)queryTerms.Count) * 0.25, 0, 1);
    }

    private static HashSet<string> Terms(string value)
    {
        return Regex.Matches(value ?? string.Empty, @"[A-Za-z0-9]+(?:[+#./-][A-Za-z0-9]+)*")
            .Select(m => m.Value.ToLowerInvariant())
            .Where(v => v.Length > 1)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
