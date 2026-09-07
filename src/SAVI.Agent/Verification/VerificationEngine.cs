using System.Text.Json;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Agent.Verification;

public class VerificationEngine : IVerificationEngine
{
    public Task<VerificationResult> VerifyAndCompareAsync(
        string query,
        IReadOnlyList<ProviderResult> results,
        CancellationToken cancellationToken = default)
    {
        var successful = results.Where(r => r.Success && r.Data != null).ToList();

        if (successful.Count == 0)
        {
            var lowerQuery = query.ToLowerInvariant();
            string fallbackSynthesis;

            if (lowerQuery.Contains("help") || lowerQuery.Contains("what can you do") || lowerQuery.Contains("capability") || lowerQuery.Contains("capabilities"))
            {
                fallbackSynthesis = "I am SAVI (Shatru's Adaptive Virtual Intelligence). I can help you with writing and analyzing code, checking live weather, calculating expressions, converting currencies, looking up crypto rates, finding research papers, exploring books, querying Wikidata entities, inspecting host diagnostics, and remembering your preferences.";
            }
            else
            {
                fallbackSynthesis = $"I looked into \"{query}\", but couldn't verify an authoritative answer right now. Rather than fabricating a response, I'll be transparent: please try rephrasing or checking another reliable source.";
            }

            return Task.FromResult(new VerificationResult
            {
                IsVerified = false,
                Confidence = 0.0,
                HasContradictions = false,
                Synthesis = fallbackSynthesis,
                Sources = Array.Empty<SourceReference>()
            });
        }

        // Deduplicate sources by URL or domain
        var allSources = successful
            .SelectMany(r => r.Sources)
            .GroupBy(s => string.IsNullOrWhiteSpace(s.Url) ? s.SourceName : s.Url)
            .Select(g => g.First())
            .ToList();

        if (successful.Count == 1)
        {
            var single = successful[0];
            return Task.FromResult(new VerificationResult
            {
                IsVerified = true,
                Confidence = single.Confidence,
                HasContradictions = false,
                Synthesis = FormatDataToText(single.Data),
                Sources = allSources
            });
        }

        // Multi-source comparison: Check for discrepancies (e.g. weather temperature difference > 4 degrees)
        bool contradiction = false;
        string? contradictionReason = null;

        var weatherTemps = new List<(string Provider, double Temp)>();
        foreach (var res in successful)
        {
            if (res.Data != null)
            {
                try
                {
                    var json = JsonSerializer.Serialize(res.Data);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("TemperatureCelsius", out var tempProp))
                    {
                        weatherTemps.Add((res.ProviderName, tempProp.GetDouble()));
                    }
                }
                catch { }
            }
        }

        if (weatherTemps.Count >= 2)
        {
            var diff = Math.Abs(weatherTemps[0].Temp - weatherTemps[1].Temp);
            if (diff > 4.0)
            {
                contradiction = true;
                contradictionReason = $"Discrepancy detected: {weatherTemps[0].Provider} reports {weatherTemps[0].Temp}°C while {weatherTemps[1].Provider} reports {weatherTemps[1].Temp}°C.";
            }
        }

        // Weighted confidence based on source authority and agreement
        var primary = successful.OrderByDescending(r => r.Confidence).First();
        var avgConfidence = successful.Average(r => r.Confidence);

        var synthesis = FormatDataToText(primary.Data);
        if (contradiction)
        {
            synthesis = $"I noticed some differing numbers between sources ({contradictionReason}). Here is the verified consensus based on our primary source:\n\n{synthesis}";
        }

        var finalConfidence = contradiction ? Math.Max(0.5, avgConfidence - 0.2) : Math.Min(1.0, avgConfidence + 0.05);

        return Task.FromResult(new VerificationResult
        {
            IsVerified = !contradiction,
            Confidence = finalConfidence,
            HasContradictions = contradiction,
            ContradictionExplanation = contradictionReason,
            Synthesis = synthesis,
            Sources = allSources
        });
    }

    private static string FormatDataToText(object? data)
    {
        if (data == null) return string.Empty;
        if (data is string str) return str;
        try
        {
            // If data contains a "Formatted" or "Message" property, prefer it
            var json = JsonSerializer.Serialize(data);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("Formatted", out var fProp))
                {
                    return fProp.GetString() ?? "";
                }
                if (root.TryGetProperty("Message", out var mProp))
                {
                    return mProp.GetString() ?? "";
                }

                var lines = new List<string>();
                foreach (var prop in root.EnumerateObject())
                {
                    lines.Add($"• **{prop.Name}**: {prop.Value}");
                }
                return string.Join("\n", lines);
            }

            if (root.ValueKind == JsonValueKind.Array)
            {
                var items = new List<string>();
                foreach (var el in root.EnumerateArray())
                {
                    if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("Title", out var tProp))
                    {
                        var title = tProp.GetString() ?? "";
                        var desc = el.TryGetProperty("Snippet", out var snProp) ? snProp.GetString() ?? "" :
                                   el.TryGetProperty("Authors", out var auProp) ? $"by {auProp.GetString()}" : "";
                        var url = el.TryGetProperty("Url", out var uProp) ? uProp.GetString() ?? "" : "";
                        items.Add($"• **{title}** {(string.IsNullOrWhiteSpace(desc) ? "" : $"— {desc}")} {(string.IsNullOrWhiteSpace(url) ? "" : $"([Link]({url}))")}");
                    }
                    else
                    {
                        items.Add($"• {el}");
                    }
                }
                return string.Join("\n", items);
            }

            return json;
        }
        catch
        {
            return data.ToString() ?? "";
        }
    }
}
