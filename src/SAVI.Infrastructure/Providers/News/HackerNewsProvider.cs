using System.Text.Json;
using SAVI.Core.Constants;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Infrastructure.Providers.News;

public class HackerNewsProvider : ICapabilityProvider
{
    private readonly HttpClient _httpClient;

    public HackerNewsProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Id => SaviConstants.Providers.HackerNews;
    public string Name => "Hacker News Developer & Tech News (Free)";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.TechNews };
    public int Priority => 15;
    public ProviderCategory Category => ProviderCategory.SpecializedPublicApi;
    public ProviderCostType CostType => ProviderCostType.FreePublic;
    public double AuthorityLevel => 0.75;
    public double AccuracyScore => 0.85;
    public double ReliabilityScore => 0.90;
    public TimeSpan TypicalLatency => TimeSpan.FromMilliseconds(400);
    public TimeSpan Timeout => TimeSpan.FromSeconds(3);

    public bool CanHandle(TaskRequest request)
    {
        return request.Capability.Equals(SaviConstants.Capabilities.TechNews, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ProviderResult> ExecuteAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = new CancellationTokenSource(Timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

            var topStoriesUrl = "https://hacker-news.firebaseio.com/v0/topstories.json";
            var topResp = await _httpClient.GetAsync(topStoriesUrl, linked.Token);

            if (!topResp.IsSuccessStatusCode)
            {
                return ProviderResult.Failed(Id, Name, $"Hacker News returned HTTP {(int)topResp.StatusCode}");
            }

            var json = await topResp.Content.ReadAsStringAsync(linked.Token);
            using var doc = JsonDocument.Parse(json);
            var storyIds = doc.RootElement.EnumerateArray().Take(4).Select(e => e.GetInt64()).ToList();

            var stories = new List<object>();
            var sources = new List<SourceReference>();

            var itemTasks = storyIds.Select(async id =>
            {
                try
                {
                    var itemResp = await _httpClient.GetAsync($"https://hacker-news.firebaseio.com/v0/item/{id}.json", linked.Token);
                    if (itemResp.IsSuccessStatusCode)
                    {
                        var itemJson = await itemResp.Content.ReadAsStringAsync(linked.Token);
                        using var itemDoc = JsonDocument.Parse(itemJson);
                        var root = itemDoc.RootElement;
                        var title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "Untitled" : "Untitled";
                        var url = root.TryGetProperty("url", out var u) ? u.GetString() ?? $"https://news.ycombinator.com/item?id={id}" : $"https://news.ycombinator.com/item?id={id}";
                        var score = root.TryGetProperty("score", out var s) ? s.GetInt32() : 0;
                        var by = root.TryGetProperty("by", out var b) ? b.GetString() ?? "" : "";
                        var comments = root.TryGetProperty("descendants", out var d) ? d.GetInt32() : 0;

                        return new
                        {
                            Id = id,
                            Title = title,
                            Url = url,
                            Score = score,
                            By = by,
                            Comments = comments
                        };
                    }
                }
                catch { }
                return null;
            });

            var fetchedStories = (await Task.WhenAll(itemTasks)).Where(s => s != null).ToList();

            foreach (var s in fetchedStories)
            {
                if (s == null) continue;
                stories.Add(s);
                sources.Add(new SourceReference
                {
                    Title = s.Title,
                    Url = s.Url,
                    SourceName = "Hacker News",
                    Snippet = $"{s.Title} ({s.Score} points, {s.Comments} comments by {s.By})",
                    ReliabilityScore = 0.85
                });
            }

            return ProviderResult.Succeeded(Id, Name, stories, confidence: 0.85, sources: sources);
        }
        catch (OperationCanceledException)
        {
            return ProviderResult.Failed(Id, Name, "Hacker News request timed out.");
        }
        catch (Exception ex)
        {
            return ProviderResult.Failed(Id, Name, $"Hacker News error: {ex.Message}");
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
            using var resp = await _httpClient.GetAsync("https://hacker-news.firebaseio.com/v0/topstories.json?limitToFirst=1&orderBy=\"$key\"", linked.Token);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
