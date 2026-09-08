using System.Text.Json;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Agent.Synthesis;

public class EvidenceAggregator
{
    public IReadOnlyList<ProviderEvidence> Aggregate(IEnumerable<ProviderResult> results)
    {
        var evidenceList = new List<ProviderEvidence>();

        foreach (var result in results)
        {
            if (!result.Success || result.Data == null)
            {
                continue;
            }

            var (content, relevantPassages, title, sourceUrl) = ExtractContent(result.Data);

            // If sourceUrl wasn't found in Data, try the result's sources
            if (string.IsNullOrWhiteSpace(sourceUrl) && result.Sources.Count > 0)
            {
                sourceUrl = result.Sources[0].Url;
            }

            if (string.IsNullOrWhiteSpace(title) && result.Sources.Count > 0)
            {
                title = result.Sources[0].Title;
            }

            if (string.IsNullOrWhiteSpace(content) && result.Sources.Count > 0)
            {
                content = string.Join("\n", result.Sources.Select(s => s.Snippet).Where(s => !string.IsNullOrWhiteSpace(s)));
            }

            evidenceList.Add(new ProviderEvidence
            {
                ProviderId = result.ProviderId,
                ProviderName = result.ProviderName,
                Title = title,
                Content = content,
                RelevantPassages = relevantPassages,
                SourceUrl = sourceUrl,
                Confidence = result.Confidence,
                AuthorityScore = result.AuthorityScore,
                FreshnessScore = result.FreshnessScore,
                IsDeterministic = result.IsDeterministic,
                RetrievedAt = result.RetrievedAt.UtcDateTime,
                Sources = result.Sources
            });
        }

        return evidenceList;
    }

    private static (string Content, IReadOnlyList<string> Passages, string Title, string SourceUrl) ExtractContent(object data)
    {
        if (data is string str)
        {
            return (str, new[] { str }, string.Empty, string.Empty);
        }

        if (data is SearchResult searchResult)
        {
            var passages = searchResult.Items
                .Select(item => $"{item.Title}: {item.Snippet}")
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();
            var content = string.Join("\n\n", passages);
            var topUrl = searchResult.Items.FirstOrDefault()?.Url ?? string.Empty;
            var topTitle = searchResult.Items.FirstOrDefault()?.Title ?? searchResult.Query;
            return (content, passages, topTitle, topUrl);
        }

        try
        {
            var json = JsonSerializer.Serialize(data);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string title = string.Empty;
            string content = string.Empty;
            string url = string.Empty;
            var passages = new List<string>();

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("Title", out var tProp) || root.TryGetProperty("title", out tProp))
                {
                    title = tProp.GetString() ?? string.Empty;
                }

                if (root.TryGetProperty("Summary", out var sProp) || root.TryGetProperty("summary", out sProp) ||
                    root.TryGetProperty("Extract", out sProp) || root.TryGetProperty("extract", out sProp) ||
                    root.TryGetProperty("Description", out sProp) || root.TryGetProperty("description", out sProp) ||
                    root.TryGetProperty("Formatted", out sProp) || root.TryGetProperty("formatted", out sProp) ||
                    root.TryGetProperty("Message", out sProp) || root.TryGetProperty("message", out sProp))
                {
                    content = sProp.GetString() ?? string.Empty;
                }

                if (root.TryGetProperty("Url", out var uProp) || root.TryGetProperty("url", out uProp) ||
                    root.TryGetProperty("PageUrl", out uProp))
                {
                    url = uProp.GetString() ?? string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(content))
                {
                    passages.Add(content);
                }

                // If content is still empty, format all non-empty properties into sentences
                if (string.IsNullOrWhiteSpace(content))
                {
                    var lines = new List<string>();
                    foreach (var prop in root.EnumerateObject())
                    {
                        var valStr = prop.Value.ToString();
                        if (!string.IsNullOrWhiteSpace(valStr))
                        {
                            lines.Add($"{prop.Name}: {valStr}");
                        }
                    }
                    content = string.Join("\n", lines);
                    passages.AddRange(lines);
                }
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                var lines = new List<string>();
                foreach (var el in root.EnumerateArray())
                {
                    lines.Add(el.ToString());
                }
                content = string.Join("\n", lines);
                passages.AddRange(lines);
            }

            return (content, passages, title, url);
        }
        catch
        {
            var fallback = data.ToString() ?? string.Empty;
            return (fallback, new[] { fallback }, string.Empty, string.Empty);
        }
    }
}
