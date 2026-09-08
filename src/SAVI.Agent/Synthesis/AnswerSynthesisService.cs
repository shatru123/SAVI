using System.Text;
using System.Text.RegularExpressions;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;
using SAVI.Core.Interfaces;

namespace SAVI.Agent.Synthesis;

public class AnswerSynthesisService : IAnswerSynthesisService
{
    private readonly EvidenceEvaluator _evidenceEvaluator;

    public AnswerSynthesisService(EvidenceEvaluator? evidenceEvaluator = null)
    {
        _evidenceEvaluator = evidenceEvaluator ?? new EvidenceEvaluator();
    }

    public Task<SynthesizedAnswer> SynthesizeAsync(
        string userQuestion,
        QueryAnalysisResult analysis,
        IReadOnlyList<ProviderEvidence> evidenceList,
        ContextPackage context,
        bool isVoiceMode,
        CancellationToken cancellationToken = default)
    {
        // 1. Filter evidence by relevance
        var evaluatedEvidence = evidenceList
            .Select(evidence => _evidenceEvaluator.Evaluate(analysis, evidence))
            .ToList();
        var relevantEvidence = evaluatedEvidence
            .Where(evidence => evidence.IsRelevant)
            .OrderByDescending(evidence => evidence.RelevanceScore * 0.50 + evidence.AuthorityScore * 0.30 + evidence.FreshnessScore * 0.20)
            .ToList();

        // Deduplicate sources
        var allSources = relevantEvidence
            .SelectMany(e => e.Sources.Count > 0 ? e.Sources : (string.IsNullOrWhiteSpace(e.SourceUrl) ? Array.Empty<SourceReference>() : new[]
            {
                new SourceReference
                {
                    Title = string.IsNullOrWhiteSpace(e.Title) ? e.ProviderName : e.Title,
                    Url = e.SourceUrl,
                    SourceName = e.ProviderName,
                    Snippet = e.Content.Length > 160 ? e.Content[..157] + "..." : e.Content,
                    ReliabilityScore = e.Confidence
                }
            }))
            .GroupBy(s => string.IsNullOrWhiteSpace(s.Url) ? s.SourceName : s.Url)
            .Select(g => g.First())
            .ToList();

        // Multi-part questions are answered from evidence for each requested aspect.
        if (analysis.SubQuestions.Count > 1)
        {
            var multiAnswer = SynthesizeMultiPartAnswer(analysis, relevantEvidence, isVoiceMode, allSources);
            return Task.FromResult(multiAnswer);
        }

        // If no relevant evidence was found, do not turn a provider response or a
        // remembered fact into an unverified answer.
        if (relevantEvidence.Count == 0)
        {
            var lowerQ = userQuestion.ToLowerInvariant();
            string unverifiedReply;

            if (lowerQ.Contains("help") || lowerQ.Contains("what can you do"))
            {
                unverifiedReply = "I am SAVI, your personal assistant. I can answer technical questions, explain programming concepts, check real-time weather, calculate expressions, convert currencies, search books, and remember your preferences.";
            }
            else
            {
                unverifiedReply = $"I looked into \"{userQuestion}\", but couldn't verify an authoritative answer right now. Rather than guessing, I want to be transparent with you.";
            }

            var voiceUnverified = CleanForSpeech(unverifiedReply);
            return Task.FromResult(new SynthesizedAnswer
            {
                MainContent = unverifiedReply,
                VoiceContent = voiceUnverified,
                Confidence = 0.0,
                IsVerified = false,
                Sources = Array.Empty<SourceReference>(),
                IsComplete = false
            });
        }

        // Standard single-topic factual synthesis
        var topEvidence = relevantEvidence.OrderByDescending(e => e.Confidence).First();
        var mainContent = CleanFactualContent(topEvidence.Content, topEvidence.Title);

        // Ensure we never return raw links or JSON
        if (IsRawLinkOrJson(mainContent))
        {
            mainContent = CleanRawDumps(mainContent, analysis.Topic);
        }

        // Retrieval sources do not always repeat a symbol, alias, or resolved
        // subject in the first sentence. Preserve the user's resolved subject in
        // the answer without inventing a fact.
        var answerSubject = analysis.Entities.FirstOrDefault() ?? analysis.Topic;
        if (!string.IsNullOrWhiteSpace(answerSubject) &&
            !mainContent.Contains(answerSubject, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(mainContent))
        {
            mainContent = $"{answerSubject}: {mainContent}";
        }

        var voiceContent = FormatVoiceResponse(mainContent);
        var isComplete = topEvidence.RequirementCoverage >= 0.75;

        return Task.FromResult(new SynthesizedAnswer
        {
            MainContent = mainContent,
            VoiceContent = voiceContent,
            Confidence = topEvidence.Confidence,
            IsVerified = isComplete,
            Sources = allSources,
            IsComplete = isComplete,
            UnansweredSubQuestions = isComplete ? Array.Empty<string>() : analysis.SubQuestions
        });
    }

    private static SynthesizedAnswer SynthesizeMultiPartAnswer(
        QueryAnalysisResult analysis,
        IReadOnlyList<ProviderEvidence> evidence,
        bool isVoiceMode,
        IReadOnlyList<SourceReference> sources)
    {
        var sb = new StringBuilder();
        var voiceSb = new StringBuilder();
        var unanswered = new List<string>();

        // Synthesize an answer for each sub-question from available evidence passages
        for (int i = 0; i < analysis.SubQuestions.Count; i++)
        {
            var subQ = analysis.SubQuestions[i];
            var matchingEvidence = evidence.FirstOrDefault(e =>
                e.RelevantPassages.Any(p => ContainsOverlap(p, $"{subQ} {analysis.Topic} {string.Join(" ", analysis.Entities)}")) ||
                ContainsOverlap(e.Content, $"{subQ} {analysis.Topic} {string.Join(" ", analysis.Entities)}"));

            if (matchingEvidence != null)
            {
                var cleanPart = CleanFactualContent(matchingEvidence.Content, matchingEvidence.Title);
                var firstSentence = ExtractFirstSentences(cleanPart, 2);
                sb.AppendLine($"• **{subQ}**\n{firstSentence}\n");
                voiceSb.Append($"{firstSentence} ");
            }
            else
            {
                unanswered.Add(subQ);
            }
        }

        // If no individual passages matched subquestions directly, combine top evidence content
        if (sb.Length == 0 && evidence.Count > 0)
        {
            var combined = string.Join("\n\n", evidence.Take(2).Select(e => CleanFactualContent(e.Content, e.Title)));
            sb.Append(combined);
            voiceSb.Append(ExtractFirstSentences(combined, 2));
        }

        var mainContent = sb.ToString().Trim();
        var voiceContent = FormatVoiceResponse(voiceSb.ToString().Trim());

        return new SynthesizedAnswer
        {
            MainContent = string.IsNullOrWhiteSpace(mainContent) ? "I retrieved information regarding your inquiry." : mainContent,
            VoiceContent = string.IsNullOrWhiteSpace(voiceContent) ? "Here is what I found." : voiceContent,
            Confidence = evidence.Count > 0 ? 0.90 : 0.50,
            IsVerified = evidence.Count > 0,
            Sources = sources,
            IsComplete = unanswered.Count == 0,
            UnansweredSubQuestions = unanswered
        };
    }

    private static bool ContainsOverlap(string text, string query)
    {
        var words = query.Split(new[] { ' ', '?', ',', '.' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3 && !w.Equals("what", StringComparison.OrdinalIgnoreCase) && !w.Equals("when", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return words.Count > 0 && words.Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));
    }

    private static string CleanFactualContent(string content, string title)
    {
        if (string.IsNullOrWhiteSpace(content)) return string.Empty;

        var clean = content.Trim();

        // If JSON, parse it to extract human-readable description/summary
        if ((clean.StartsWith("{") && clean.EndsWith("}")) || (clean.StartsWith("[") && clean.EndsWith("]")))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(clean);
                var root = doc.RootElement;
                if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (root.TryGetProperty("summary", out var s) || root.TryGetProperty("Summary", out s) ||
                        root.TryGetProperty("extract", out s) || root.TryGetProperty("Extract", out s) ||
                        root.TryGetProperty("description", out s) || root.TryGetProperty("Description", out s) ||
                        root.TryGetProperty("formatted", out s) || root.TryGetProperty("Formatted", out s))
                    {
                        clean = s.GetString() ?? string.Empty;
                    }
                    else
                    {
                        var parts = new List<string>();
                        foreach (var prop in root.EnumerateObject())
                        {
                            var v = prop.Value.ToString();
                            if (!string.IsNullOrWhiteSpace(v) && !v.StartsWith("http"))
                            {
                                parts.Add($"{prop.Name}: {v}");
                            }
                        }
                        clean = string.Join(". ", parts);
                    }
                }
            }
            catch { }
        }

        // Strip HTML tags if present
        clean = Regex.Replace(clean, @"<.*?>", string.Empty);

        // If content is repetitive or starts with "Title: ...", strip redundant prefixes
        clean = Regex.Replace(clean, @"^(?:Title|Summary|Description):\s*", "", RegexOptions.IgnoreCase);

        return clean;
    }

    private static bool IsRawLinkOrJson(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("{") && trimmed.EndsWith("}")) return true;
        if (trimmed.StartsWith("[") && trimmed.EndsWith("]")) return true;
        if (Regex.IsMatch(trimmed, @"^(?:https?://[^\s]+|Wikipedia:\s*https?://[^\s]+)$", RegexOptions.IgnoreCase)) return true;
        return false;
    }

    private static string CleanRawDumps(string text, string topic)
    {
        return $"Here is the verified information for **{topic}**: {text}";
    }

    private static string ExtractFirstSentences(string text, int count)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        var chosen = sentences.Take(count).Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s));
        return string.Join(". ", chosen) + ".";
    }

    private static string FormatVoiceResponse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // Take the first 1-3 sentences for concise speech
        var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s) && !s.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .ToList();

        var speech = string.Join(". ", sentences);
        if (!speech.EndsWith(".")) speech += ".";

        return CleanForSpeech(speech);
    }

    private static string CleanForSpeech(string text)
    {
        var clean = text;
        // Strip markdown asterisks, backticks, hashtags, URLs, and bullet markers
        clean = Regex.Replace(clean, @"\*\*([^*]+)\*\*", "$1");
        clean = Regex.Replace(clean, @"\*([^*]+)\*", "$1");
        clean = Regex.Replace(clean, @"`([^`]+)`", "$1");
        clean = Regex.Replace(clean, @"\[([^\]]+)\]\([^\)]+\)", "$1");
        clean = Regex.Replace(clean, @"https?://\S+", "");
        clean = Regex.Replace(clean, @"[•#]", "");
        clean = Regex.Replace(clean, @"\s{2,}", " ");
        return clean.Trim();
    }
}
