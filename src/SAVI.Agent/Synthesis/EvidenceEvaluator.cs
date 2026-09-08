using System.Text.RegularExpressions;
using SAVI.Core.Models;

namespace SAVI.Agent.Synthesis;

/// <summary>
/// Scores evidence against the actual request. Provider success is not evidence of
/// relevance: a result must refer to the requested entity and cover the requested
/// information before it can be used in an answer.
/// </summary>
public sealed class EvidenceEvaluator
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "are", "be", "by", "can", "do", "does", "for", "from", "how",
        "i", "in", "is", "it", "its", "of", "on", "or", "the", "this", "to", "was", "what",
        "when", "where", "which", "who", "why", "with", "about", "tell", "me", "please", "explain"
    };

    public ProviderEvidence Evaluate(QueryAnalysisResult analysis, ProviderEvidence evidence)
    {
        if (evidence.IsDeterministic && !string.IsNullOrWhiteSpace(evidence.Content))
        {
            return evidence with
            {
                RelevanceScore = 1.0,
                AuthorityScore = 1.0,
                FreshnessScore = 1.0,
                RequirementCoverage = 1.0,
                IsRelevant = true,
                RejectionReason = null
            };
        }

        var queryTerms = Tokenize(string.Join(" ", new[]
        {
            analysis.CleanPrompt,
            analysis.Topic,
            analysis.CanonicalLookupQuery,
            string.Join(" ", analysis.Entities)
        }));
        var entityTerms = Tokenize(string.Join(" ", analysis.Entities.Append(analysis.Topic)));
        var titleTerms = Tokenize(evidence.Title);
        var contentTerms = Tokenize(string.Join(" ", evidence.Content, string.Join(" ", evidence.RelevantPassages)));

        var queryOverlap = Overlap(queryTerms, contentTerms, titleTerms);
        var entityOverlap = entityTerms.Count == 0 ? 0 : Overlap(entityTerms, contentTerms, titleTerms);
        var titleOverlap = Overlap(entityTerms.Count > 0 ? entityTerms : queryTerms, titleTerms);
        var requirementCoverage = RequirementCoverage(analysis, evidence);
        var authority = Math.Clamp(Math.Max(evidence.AuthorityScore, evidence.Sources.Count == 0
            ? evidence.Confidence
            : evidence.Sources.Max(s => s.ReliabilityScore)), 0, 1);
        var freshness = CalculateFreshness(analysis, evidence);

        // Entity-bearing questions require entity agreement. This generic rule rejects
        // disambiguation accidents without naming any specific entity.
        var entityMismatch = entityTerms.Count > 0 && entityOverlap < 0.20 && titleOverlap < 0.20;
        var domainMismatch = HasConflictingDomain(queryTerms, contentTerms);
        var relevance = Math.Clamp(
            queryOverlap * 0.30 + entityOverlap * 0.35 + titleOverlap * 0.15 + requirementCoverage * 0.20,
            0, 1);
        var isRelevant = !entityMismatch && !domainMismatch && relevance >= 0.35 && requirementCoverage >= (analysis.InformationRequirements.Count > 0 ? 0.15 : 0);

        return evidence with
        {
            RelevanceScore = relevance,
            AuthorityScore = authority,
            FreshnessScore = freshness,
            RequirementCoverage = requirementCoverage,
            IsRelevant = isRelevant,
            RejectionReason = isRelevant
                ? null
                : entityMismatch
                    ? "The result does not refer to the requested entity."
                    : domainMismatch
                        ? "The result belongs to a different domain than the request."
                    : "The result does not sufficiently answer the request."
        };
    }

    private static bool HasConflictingDomain(IReadOnlySet<string> queryTerms, IReadOnlySet<string> evidenceTerms)
    {
        var domainGroups = new[]
        {
            new HashSet<string>(new[] { "programming", "language", "software", "framework", "compiler", "code", "api", "database", "protocol" }, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(new[] { "fruit", "food", "tree", "orchard", "nutrition" }, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(new[] { "company", "corporation", "stock", "business", "inc", "market" }, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(new[] { "corrosion", "iron", "oxide", "moisture", "metal" }, StringComparer.OrdinalIgnoreCase)
        };

        var queryDomains = domainGroups.Where(group => queryTerms.Any(group.Contains)).ToList();
        var evidenceDomains = domainGroups.Where(group => evidenceTerms.Any(group.Contains)).ToList();
        return queryDomains.Count > 0 && evidenceDomains.Count > 0 &&
               !queryDomains.Any(queryDomain => evidenceDomains.Contains(queryDomain));
    }

    private static double RequirementCoverage(QueryAnalysisResult analysis, ProviderEvidence evidence)
    {
        if (analysis.InformationRequirements.Count == 0) return 1;
        if (analysis.InformationRequirements.Any(requirement => requirement.Equals("answer", StringComparison.OrdinalIgnoreCase)))
        {
            return 1;
        }
        var text = string.Join(" ", evidence.Title, evidence.Content, string.Join(" ", evidence.RelevantPassages));
        var covered = analysis.InformationRequirements.Count(requirement => requirement.ToLowerInvariant() switch
        {
            "definition" => text.Length > 20,
            "creator" => ContainsAny(text, "creator", "created", "developed", "invented", "founded", "author"),
            "date" => ContainsAny(text, "date", "year", "released", "founded", "introduced"),
            "purpose" => ContainsAny(text, "purpose", "used", "use", "benefit", "advantage", "popular", "why"),
            "how" => ContainsAny(text, "how", "works", "process", "step"),
            "comparison" => ContainsAny(text, "difference", "versus", "compared", "whereas"),
            _ => text.Contains(requirement, StringComparison.OrdinalIgnoreCase)
        });
        return (double)covered / analysis.InformationRequirements.Count;

        static bool ContainsAny(string value, params string[] terms) => terms.Any(value.Contains);
    }

    private static double CalculateFreshness(QueryAnalysisResult analysis, ProviderEvidence evidence)
    {
        var retrieved = evidence.Sources.Count > 0
            ? evidence.Sources.Max(s => s.RetrievedAt)
            : new DateTimeOffset(evidence.RetrievedAt, TimeSpan.Zero);
        var age = DateTimeOffset.UtcNow - retrieved;
        if (!analysis.RequiresFreshness) return 1.0;
        if (age <= TimeSpan.FromHours(1)) return 1.0;
        if (age <= TimeSpan.FromDays(1)) return 0.85;
        if (age <= TimeSpan.FromDays(7)) return 0.60;
        return 0.25;
    }

    private static double Overlap(IReadOnlySet<string> expected, IReadOnlySet<string> actual, IReadOnlySet<string>? title = null)
    {
        if (expected.Count == 0) return 0;
        var matches = (double)expected.Count(actual.Contains);
        if (title != null) matches += expected.Count(title.Contains) * 0.5;
        return Math.Clamp(matches / expected.Count, 0, 1);
    }

    private static HashSet<string> Tokenize(string text)
    {
        return Regex.Matches(text ?? string.Empty, @"[A-Za-z0-9]+(?:[+#./-][A-Za-z0-9]+)*")
            .Select(m => m.Value.Trim('.', '/', '-'))
            .Where(token => token.Length > 1 && !StopWords.Contains(token))
            .Select(token => token.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
