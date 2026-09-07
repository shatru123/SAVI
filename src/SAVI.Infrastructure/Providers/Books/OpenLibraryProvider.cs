using System.Text.Json;
using SAVI.Core.Constants;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Infrastructure.Providers.Books;

public class OpenLibraryProvider : ICapabilityProvider
{
    private readonly HttpClient _httpClient;

    public OpenLibraryProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Id => SaviConstants.Providers.OpenLibrary;
    public string Name => "Open Library Books & Authors (Free)";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.Books };
    public int Priority => 15;
    public ProviderCategory Category => ProviderCategory.SpecializedPublicApi;
    public ProviderCostType CostType => ProviderCostType.FreePublic;
    public double AuthorityLevel => 0.90;
    public double AccuracyScore => 0.90;
    public double ReliabilityScore => 0.90;
    public TimeSpan TypicalLatency => TimeSpan.FromMilliseconds(600);
    public TimeSpan Timeout => TimeSpan.FromSeconds(3);

    public bool CanHandle(TaskRequest request)
    {
        return request.Capability.Equals(SaviConstants.Capabilities.Books, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ProviderResult> ExecuteAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        var query = request.Parameters.GetValueOrDefault("query") ??
                    request.Parameters.GetValueOrDefault("title") ??
                    request.Parameters.GetValueOrDefault("author") ??
                    request.Prompt;

        if (string.IsNullOrWhiteSpace(query))
        {
            return ProviderResult.Failed(Id, Name, "Query is empty.");
        }

        try
        {
            var url = $"https://openlibrary.org/search.json?q={Uri.EscapeDataString(query.Trim())}&limit=3";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("SAVI-Companion/1.0");

            using var cts = new CancellationTokenSource(Timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
            using var resp = await _httpClient.SendAsync(req, linked.Token);

            if (!resp.IsSuccessStatusCode)
            {
                return ProviderResult.Failed(Id, Name, $"Open Library returned HTTP {(int)resp.StatusCode}");
            }

            var json = await resp.Content.ReadAsStringAsync(linked.Token);
            using var doc = JsonDocument.Parse(json);

            var docs = doc.RootElement.GetProperty("docs");
            if (docs.GetArrayLength() == 0)
            {
                return ProviderResult.Failed(Id, Name, $"No books found for \"{query}\".");
            }

            var books = new List<object>();
            var sources = new List<SourceReference>();

            foreach (var b in docs.EnumerateArray())
            {
                var title = b.TryGetProperty("title", out var tProp) ? tProp.GetString() ?? "Unknown Title" : "Unknown Title";
                var firstPublishYear = b.TryGetProperty("first_publish_year", out var yrProp) ? yrProp.GetInt32().ToString() : "N/A";
                var key = b.TryGetProperty("key", out var kProp) ? kProp.GetString() ?? "" : "";
                var bookUrl = string.IsNullOrWhiteSpace(key) ? "https://openlibrary.org" : $"https://openlibrary.org{key}";

                var authors = new List<string>();
                if (b.TryGetProperty("author_name", out var aProp))
                {
                    foreach (var a in aProp.EnumerateArray())
                    {
                        var name = a.GetString();
                        if (!string.IsNullOrWhiteSpace(name)) authors.Add(name);
                    }
                }
                var authorSummary = authors.Count > 0 ? string.Join(", ", authors.Take(2)) : "Unknown Author";

                books.Add(new
                {
                    Title = title,
                    Author = authorSummary,
                    FirstPublishYear = firstPublishYear,
                    OpenLibraryUrl = bookUrl
                });

                sources.Add(new SourceReference
                {
                    Title = title,
                    Url = bookUrl,
                    SourceName = "Open Library",
                    Snippet = $"{title} by {authorSummary} (First published: {firstPublishYear})",
                    ReliabilityScore = 0.90
                });
            }

            return ProviderResult.Succeeded(Id, Name, books, confidence: 0.90, sources: sources);
        }
        catch (OperationCanceledException)
        {
            return ProviderResult.Failed(Id, Name, "Open Library request timed out.");
        }
        catch (Exception ex)
        {
            return ProviderResult.Failed(Id, Name, $"Open Library error: {ex.Message}");
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://openlibrary.org/search.json?q=dune&limit=1");
            req.Headers.UserAgent.ParseAdd("SAVI-Companion/1.0");
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
