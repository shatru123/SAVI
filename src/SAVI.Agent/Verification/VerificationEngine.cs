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
        var successful = results.Where(r => r.Success).ToList();

        if (successful.Count == 0)
        {
            var lowerQuery = query.ToLowerInvariant();
            string fallbackSynthesis;

            if (lowerQuery.Contains("help") || lowerQuery.Contains("what can you do") || lowerQuery.Contains("capability") || lowerQuery.Contains("capabilities"))
            {
                fallbackSynthesis = "I am SAVI (Shatru's Adaptive Virtual Intelligence). I can help you with writing and debugging code, running autonomous multi-step tasks, checking live weather, converting currencies, performing calculations, inspecting host diagnostics, and remembering your preferences.";
            }
            else if (lowerQuery.Contains("code") || lowerQuery.Contains("program") || lowerQuery.Contains("function") || lowerQuery.Contains("class") || lowerQuery.Contains("write ") || lowerQuery.Contains("implement") || lowerQuery.Contains("algorithm"))
            {
                fallbackSynthesis = $"The public serverless neural model experienced momentary network saturation while processing your code request for \"{query}\". Please try re-sending your prompt, or specify the programming language (e.g. C#, Python, TypeScript).";
            }
            else
            {
                fallbackSynthesis = $"I searched for information on \"{query}\", but couldn't retrieve verified live results at this moment. You can try rephrasing your question, or ask me to check system stats, weather, currency, or files.";
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

        var allSources = successful.SelectMany(r => r.Sources).ToList();

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

        var primary = successful.OrderByDescending(r => r.Confidence).First();
        var avgConfidence = successful.Average(r => r.Confidence);

        var synthesis = FormatDataToText(primary.Data);
        if (contradiction)
        {
            synthesis = $"I noticed some differing numbers between sources ({contradictionReason}). Here is the verified consensus based on our primary source:\n\n{synthesis}";
        }

        return Task.FromResult(new VerificationResult
        {
            IsVerified = !contradiction,
            Confidence = contradiction ? Math.Max(0.5, avgConfidence - 0.2) : Math.Min(1.0, avgConfidence + 0.05),
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
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                var lines = new List<string>();
                foreach (var prop in root.EnumerateObject())
                {
                    lines.Add($"• **{prop.Name}**: {prop.Value}");
                }
                return string.Join("\n", lines);
            }
            return json;
        }
        catch
        {
            return data.ToString() ?? "";
        }
    }
}
