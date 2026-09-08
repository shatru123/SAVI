using System.Text.Json;
using System.Text.RegularExpressions;
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
        var successful = results
            .Where(r => r.Success && r.Data != null)
            .Where(r => IsResultRelevant(query, r))
            .ToList();

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

        // Compare structured values when available, then fall back to numeric values
        // with units. This remains domain-agnostic and avoids trusting the first
        // provider merely because it responded first.
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

        if (!contradiction)
        {
            var numericValues = successful
                .Select(r => (Provider: r.ProviderName, Values: ExtractComparableNumbers(r.Data)))
                .Where(x => x.Values.Count > 0)
                .ToList();

            if (numericValues.Count >= 2)
            {
                var commonLength = numericValues.Min(x => x.Values.Count);
                for (var i = 0; i < commonLength && !contradiction; i++)
                {
                    var values = numericValues.Select(x => x.Values[i]).ToList();
                    var spread = values.Max() - values.Min();
                    var tolerance = Math.Max(0.01, Math.Abs(values.Average()) * 0.02);
                    if (spread > tolerance)
                    {
                        contradiction = true;
                        contradictionReason = $"Sources disagree on a reported value: {string.Join(", ", numericValues.Select(x => $"{x.Provider} reports {x.Values[i]}"))}.";
                    }
                }
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

    private static bool IsResultRelevant(string query, ProviderResult result)
    {
        // Local deterministic providers often return a scalar with no external
        // citation. Their provider capability is already selected by routing, so
        // absence of source text is not evidence of irrelevance.
        if (result.Sources.Count == 0) return true;
        var queryTerms = Tokenize(query);
        if (queryTerms.Count == 0) return true;
        var evidence = string.Join(" ", result.Sources.Select(s => $"{s.Title} {s.Snippet}")) + " " + FormatDataToText(result.Data);
        var evidenceTerms = Tokenize(evidence);
        var overlap = queryTerms.Count(evidenceTerms.Contains);
        // Interrogative words are intentionally excluded by Tokenize. Require a
        // meaningful match for entity-bearing and retrieval questions.
        return overlap >= Math.Max(1, (int)Math.Ceiling(queryTerms.Count * 0.15));
    }

    private static HashSet<string> Tokenize(string text)
    {
        var stopWords = new HashSet<string>(new[] { "what", "is", "are", "the", "a", "an", "of", "to", "in", "on", "for", "who", "when", "where", "why", "how", "tell", "me", "about", "current", "please" }, StringComparer.OrdinalIgnoreCase);
        return Regex.Matches(text ?? string.Empty, @"[A-Za-z0-9]+(?:[+#./-][A-Za-z0-9]+)*")
            .Select(m => m.Value.ToLowerInvariant())
            .Where(token => token.Length > 1 && !stopWords.Contains(token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<double> ExtractComparableNumbers(object? data)
    {
        var text = FormatDataToText(data);
        return Regex.Matches(text, @"(?<![A-Za-z])[-+]?\d+(?:\.\d+)?")
            .Select(match => double.TryParse(match.Value, out var value) ? (double?)value : null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();
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
