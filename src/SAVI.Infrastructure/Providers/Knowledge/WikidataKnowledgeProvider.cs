using System.Text.Json;
using SAVI.Core.Constants;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Infrastructure.Providers.Knowledge;

public class WikidataKnowledgeProvider : ICapabilityProvider
{
    private readonly HttpClient _httpClient;

    public WikidataKnowledgeProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public string Id => SaviConstants.Providers.Wikidata;
    public string Name => "Wikidata Structured Entity Knowledge (Free)";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.Entity, SaviConstants.Capabilities.Knowledge };
    public int Priority => 15;
    public ProviderCategory Category => ProviderCategory.KnowledgeBase;
    public ProviderCostType CostType => ProviderCostType.FreePublic;
    public double AuthorityLevel => 0.90;
    public double AccuracyScore => 0.95;
    public double ReliabilityScore => 0.95;
    public TimeSpan TypicalLatency => TimeSpan.FromMilliseconds(500);
    public TimeSpan Timeout => TimeSpan.FromSeconds(3);

    public bool CanHandle(TaskRequest request)
    {
        return request.Capability.Equals(SaviConstants.Capabilities.Entity, StringComparison.OrdinalIgnoreCase) ||
               (request.Capability.Equals(SaviConstants.Capabilities.Knowledge, StringComparison.OrdinalIgnoreCase) &&
                request.Parameters.ContainsKey("entity"));
    }

    public async Task<ProviderResult> ExecuteAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        var query = request.Parameters.GetValueOrDefault("entity") ??
                    request.Parameters.GetValueOrDefault("topic") ??
                    request.Parameters.GetValueOrDefault("query") ??
                    request.Prompt;

        if (string.IsNullOrWhiteSpace(query))
        {
            return ProviderResult.Failed(Id, Name, "Query is empty.");
        }

        try
        {
            var url = $"https://www.wikidata.org/w/api.php?action=wbsearchentities&search={Uri.EscapeDataString(query.Trim())}&language=en&format=json&limit=3";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("SAVI-Companion/1.0 (https://github.com/shatru123/SAVI; contact: open-assistant@savi.local)");

            using var cts = new CancellationTokenSource(Timeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
            using var resp = await _httpClient.SendAsync(req, linked.Token);

            if (!resp.IsSuccessStatusCode)
            {
                return ProviderResult.Failed(Id, Name, $"Wikidata returned HTTP {(int)resp.StatusCode}");
            }

            var json = await resp.Content.ReadAsStringAsync(linked.Token);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("search", out var searchArray) && searchArray.GetArrayLength() > 0)
            {
                var first = searchArray[0];
                var entityId = first.GetProperty("id").GetString() ?? "";
                var label = first.GetProperty("label").GetString() ?? query;
                var description = first.TryGetProperty("description", out var dProp) ? dProp.GetString() ?? "" : "";
                var conceptUri = first.TryGetProperty("concepturi", out var cProp) ? cProp.GetString() ?? $"https://www.wikidata.org/wiki/{entityId}" : $"https://www.wikidata.org/wiki/{entityId}";

                var resultData = new
                {
                    EntityId = entityId,
                    Label = label,
                    Description = description,
                    Url = conceptUri,
                    Formatted = $"**{label}** ({entityId}): {description}"
                };

                var source = new SourceReference
                {
                    Title = $"{label} — Wikidata",
                    Url = conceptUri,
                    SourceName = "Wikidata Knowledge Base",
                    Snippet = $"{label}: {description}",
                    ReliabilityScore = 0.95
                };

                return ProviderResult.Succeeded(Id, Name, resultData, confidence: 0.95, sources: new[] { source });
            }

            return ProviderResult.Failed(Id, Name, $"No Wikidata structured entity found for \"{query}\".");
        }
        catch (OperationCanceledException)
        {
            return ProviderResult.Failed(Id, Name, "Wikidata request timed out.");
        }
        catch (Exception ex)
        {
            return ProviderResult.Failed(Id, Name, $"Wikidata error: {ex.Message}");
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://www.wikidata.org/w/api.php?action=wbsearchentities&search=Earth&language=en&format=json&limit=1");
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
