using System.Text.RegularExpressions;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;

namespace SAVI.Agent.Understanding;

public class QueryUnderstandingService : IQueryUnderstandingService
{
    private static readonly Dictionary<string, (string CanonicalName, string Domain, string Category)> TechnicalDictionary =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["c#"] = ("C# programming language", "Programming", "Language"),
            ["c sharp"] = ("C# programming language", "Programming", "Language"),
            ["c++"] = ("C++ programming language", "Programming", "Language"),
            ["f#"] = ("F# programming language", "Programming", "Language"),
            [".net"] = (".NET platform", "Programming", "Framework"),
            [".net core"] = (".NET Core platform", "Programming", "Framework"),
            [".net 10"] = (".NET 10 runtime", "Programming", "Framework"),
            [".net 9"] = (".NET 9 runtime", "Programming", "Framework"),
            [".net 8"] = (".NET 8 runtime", "Programming", "Framework"),
            ["asp.net"] = ("ASP.NET web framework", "Programming", "Framework"),
            ["asp.net core"] = ("ASP.NET Core web framework", "Programming", "Framework"),
            ["node.js"] = ("Node.js JavaScript runtime", "Programming", "Runtime"),
            ["nodejs"] = ("Node.js JavaScript runtime", "Programming", "Runtime"),
            ["react.js"] = ("React JavaScript library", "Programming", "Library"),
            ["reactjs"] = ("React JavaScript library", "Programming", "Library"),
            ["vue.js"] = ("Vue.js JavaScript framework", "Programming", "Framework"),
            ["angular.js"] = ("Angular web framework", "Programming", "Framework"),
            ["next.js"] = ("Next.js React framework", "Programming", "Framework"),
            ["typescript"] = ("TypeScript programming language", "Programming", "Language"),
            ["javascript"] = ("JavaScript programming language", "Programming", "Language"),
            ["python"] = ("Python programming language", "Programming", "Language"),
            ["java"] = ("Java programming language", "Programming", "Language"),
            ["golang"] = ("Go programming language", "Programming", "Language"),
            ["rust"] = ("Rust programming language", "Programming", "Language"),
            ["kotlin"] = ("Kotlin programming language", "Programming", "Language"),
            ["swift"] = ("Swift programming language", "Programming", "Language"),
            ["sql"] = ("SQL database query language", "Programming", "Database"),
            ["nosql"] = ("NoSQL database technology", "Programming", "Database"),
            ["postgresql"] = ("PostgreSQL relational database", "Programming", "Database"),
            ["mongodb"] = ("MongoDB document database", "Programming", "Database"),
            ["redis"] = ("Redis in-memory data store", "Programming", "Database"),
            ["graphql"] = ("GraphQL query language and API runtime", "Programming", "API"),
            ["grpc"] = ("gRPC high-performance RPC framework", "Programming", "Networking"),
            ["rest"] = ("REST architectural style for web APIs", "Programming", "API"),
            ["oauth 2.0"] = ("OAuth 2.0 authorization framework", "Security", "Standard"),
            ["oauth2"] = ("OAuth 2.0 authorization framework", "Security", "Standard"),
            ["oauth"] = ("OAuth authorization framework", "Security", "Standard"),
            ["jwt"] = ("JSON Web Token (JWT) security standard", "Security", "Standard"),
            ["openid connect"] = ("OpenID Connect identity layer", "Security", "Standard"),
            ["aes-128"] = ("AES-128 cryptographic encryption standard", "Security", "Cryptography"),
            ["aes-256"] = ("AES-256 cryptographic encryption standard", "Security", "Cryptography"),
            ["rsa"] = ("RSA public-key cryptosystem", "Security", "Cryptography"),
            ["sha-256"] = ("SHA-256 cryptographic hash function", "Security", "Cryptography"),
            ["3gpp"] = ("3GPP telecommunications standards organization", "Telecommunications", "Standard"),
            ["5g-aka"] = ("5G-AKA authentication protocol", "Telecommunications", "Security"),
            ["http/2"] = ("HTTP/2 network protocol", "Networking", "Protocol"),
            ["http/3"] = ("HTTP/3 network protocol", "Networking", "Protocol"),
            ["websocket"] = ("WebSocket communication protocol", "Networking", "Protocol"),
            ["docker"] = ("Docker container platform", "DevOps", "Platform"),
            ["kubernetes"] = ("Kubernetes container orchestration system", "DevOps", "Platform"),
            ["git"] = ("Git distributed version control system", "DevOps", "Tool"),
            ["github"] = ("GitHub software development platform", "DevOps", "Platform"),
            ["savi"] = ("SAVI", "General", "Assistant")
        };

    private static readonly Regex GenericTechnicalRegex = new(
        @"(?:\b[A-Za-z]+[#\+]+\b|\.[A-Za-z0-9]+(?:\s+\d+)?\b|\b[A-Za-z0-9]+\.js\b|\b[A-Za-z0-9]+-[A-Za-z0-9]+\b|\b[A-Za-z0-9]+/[A-Za-z0-9]+\b|\b[A-Za-z0-9_]+<[A-Za-z0-9_,\s]+>)",
        RegexOptions.Compiled);

    private static readonly Regex PronounRegex = new(
        @"\b(?:it|its|they|them|their|this|that|this language|this framework|this tool)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ISaviSelfKnowledgeService? _selfKnowledgeService;

    public QueryUnderstandingService(ISaviSelfKnowledgeService? selfKnowledgeService = null)
    {
        _selfKnowledgeService = selfKnowledgeService;
    }

    public QueryAnalysisResult Analyze(string prompt, ContextPackage? context = null)
    {
        var rawPrompt = prompt ?? string.Empty;
        var clean = rawPrompt.Trim();
        var lower = clean.ToLowerInvariant();

        // 0. Check for policy override
        VerificationPolicy? policyOverride = null;
        if (lower.Contains("quick answer") || lower.Contains("fast answer") || lower.Contains("quickly"))
        {
            policyOverride = VerificationPolicy.Fast;
        }
        else if (lower.Contains("verify carefully") || lower.Contains("cross check") || lower.Contains("verified answer") || lower.Contains("cross-check"))
        {
            policyOverride = VerificationPolicy.Verified;
        }

        // 1. Identify entities and technical terms
        var detectedEntities = new List<string>();
        bool isTechnical = false;
        string domain = "General";
        string canonicalQuery = clean;

        // Check against dictionary
        foreach (var (key, (canonical, dictDomain, _)) in TechnicalDictionary)
        {
            var pattern = $@"(?<=^|[\s,;?!""']){Regex.Escape(key)}(?=[\s,;?!""']|$)";
            var m = Regex.Match(clean, pattern, RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var val = m.Value;
                if (!detectedEntities.Contains(val, StringComparer.OrdinalIgnoreCase))
                {
                    detectedEntities.Add(val);
                }
                isTechnical = true;
                domain = dictDomain;
            }
        }

        // Check generic regex patterns for programming languages, generics, versions
        var matches = GenericTechnicalRegex.Matches(clean);
        foreach (Match match in matches)
        {
            var val = match.Value;
            if (!detectedEntities.Contains(val, StringComparer.OrdinalIgnoreCase))
            {
                detectedEntities.Add(val);
                isTechnical = true;
                if (domain == "General") domain = "Programming";
            }
        }

        // 2. Resolve pronouns from conversation context if needed
        string? resolvedContextQuery = null;
        var hasPronouns = PronounRegex.IsMatch(clean);
        string? priorSubject = ExtractPriorSubjectFromContext(context);

        if (hasPronouns && !string.IsNullOrWhiteSpace(priorSubject))
        {
            // Resolve pronouns: replace "it", "its creator", etc. with the prior subject
            resolvedContextQuery = ResolvePronounsInText(clean, priorSubject);
            if (detectedEntities.Count == 0 && !string.IsNullOrWhiteSpace(priorSubject))
            {
                detectedEntities.Add(priorSubject);
                if (TechnicalDictionary.ContainsKey(priorSubject) || GenericTechnicalRegex.IsMatch(priorSubject))
                {
                    isTechnical = true;
                    domain = "Programming";
                }
            }
        }

        // Determine main topic
        string primaryEntity = detectedEntities.FirstOrDefault() ?? priorSubject ?? string.Empty;
        string topic = primaryEntity;

        if (!string.IsNullOrWhiteSpace(primaryEntity) && TechnicalDictionary.TryGetValue(primaryEntity, out var techMeta))
        {
            canonicalQuery = techMeta.CanonicalName;
            topic = techMeta.CanonicalName;
        }
        else if (string.IsNullOrWhiteSpace(topic))
        {
            topic = ExtractTopicFallback(clean);
            canonicalQuery = topic;
        }

        // 3. Decompose multi-part questions
        var subQuestions = DecomposeMultiPartQuestions(clean, resolvedContextQuery, primaryEntity);

        // Determine intent
        var intent = DetermineIntent(lower, isTechnical, subQuestions.Count > 1);
        var requirements = ExtractInformationRequirements(lower);
        var requiresFreshness = RequiresFreshness(lower);
        var retrievalQueries = BuildRetrievalQueries(clean, resolvedContextQuery, canonicalQuery, requirements);
        var isComputational = Regex.IsMatch(lower, @"\b(?:calculate|compute|convert|how much|equation|solve)\b|\d+\s*[+\-*/^]\s*\d+");
        var isAmbiguous = detectedEntities.Count == 0 &&
                          Regex.IsMatch(clean, @"^(?:what is|who is|tell me about|explain)\s+[^?]{1,40}\??$", RegexOptions.IgnoreCase) &&
                          !Regex.IsMatch(lower, @"\b(?:programming|language|company|fruit|weather|capital|president|history|protocol|database|framework)\b");

        var effectivePrompt = resolvedContextQuery ?? clean;
        var selfKnowledgeMatch = _selfKnowledgeService?.MatchQuery(clean, context) ??
                                 _selfKnowledgeService?.MatchQuery(effectivePrompt, context);
        bool isSelfKnowledge = selfKnowledgeMatch != null;
        SelfKnowledgeCategory? selfKnowledgeCategory = selfKnowledgeMatch?.Category;

        return new QueryAnalysisResult
        {
            RawPrompt = rawPrompt,
            CleanPrompt = clean,
            Topic = topic,
            Intent = intent,
            Domain = domain,
            Entities = detectedEntities,
            IsTechnical = isTechnical,
            SubQuestions = subQuestions,
            ResolvedContextQuery = resolvedContextQuery,
            CanonicalLookupQuery = canonicalQuery,
            RetrievalQueries = retrievalQueries,
            InformationRequirements = requirements,
            RequiresFreshness = requiresFreshness,
            IsComputational = isComputational,
            IsAmbiguous = isAmbiguous,
            IsSelfKnowledge = isSelfKnowledge,
            SelfKnowledgeCategory = selfKnowledgeCategory,
            Confidence = isTechnical ? 0.95 : 0.90,
            PolicyOverride = policyOverride
        };
    }

    private static IReadOnlyList<string> ExtractInformationRequirements(string lowerPrompt)
    {
        var requirements = new List<string>();
        if (Regex.IsMatch(lowerPrompt, @"\b(?:what is|what are|define|meaning|explain)\b")) requirements.Add("definition");
        if (Regex.IsMatch(lowerPrompt, @"\b(?:who|creator|invented|founded|author|built|developed)\b")) requirements.Add("creator");
        if (Regex.IsMatch(lowerPrompt, @"\b(?:when|year|date|released|founded)\b")) requirements.Add("date");
        if (Regex.IsMatch(lowerPrompt, @"\b(?:why|purpose|used|popular|benefit|advantage)\b")) requirements.Add("purpose");
        if (Regex.IsMatch(lowerPrompt, @"\b(?:how|work|works|steps|process)\b")) requirements.Add("how");
        if (Regex.IsMatch(lowerPrompt, @"\b(?:difference|compare|versus|\bvs\b)\b")) requirements.Add("comparison");
        return requirements.Count == 0 ? new[] { "answer" } : requirements.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool RequiresFreshness(string lowerPrompt)
    {
        return Regex.IsMatch(lowerPrompt,
            @"\b(?:current|currently|latest|recent|today|now|live|real[- ]?time|forecast|weather|exchange rate|price|news|happened)\b",
            RegexOptions.IgnoreCase);
    }

    private static IReadOnlyList<string> BuildRetrievalQueries(
        string cleanPrompt,
        string? resolvedContextQuery,
        string canonicalQuery,
        IReadOnlyList<string> requirements)
    {
        var queries = new List<string>();
        AddIfNotEmpty(queries, resolvedContextQuery);
        AddIfNotEmpty(queries, cleanPrompt);
        if (!string.Equals(canonicalQuery, cleanPrompt, StringComparison.OrdinalIgnoreCase))
        {
            AddIfNotEmpty(queries, $"{canonicalQuery} {string.Join(" ", requirements)}");
        }
        return queries.Distinct(StringComparer.OrdinalIgnoreCase).Take(4).ToArray();

        static void AddIfNotEmpty(List<string> values, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) values.Add(value.Trim());
        }
    }

    private static string ExtractPriorSubjectFromContext(ContextPackage? context)
    {
        if (context?.RecentMessages == null || context.RecentMessages.Count == 0)
        {
            return string.Empty;
        }

        // Pass 1: Prioritize User inquiries (e.g. "Tell me about Tokyo", "What is C#?", "Weather in Pune")
        for (int i = context.RecentMessages.Count - 1; i >= 0; i--)
        {
            var msg = context.RecentMessages[i];
            if (msg.Role != MessageRole.User) continue;

            var content = msg.Content;

            // Check dictionary terms first
            foreach (var (key, _) in TechnicalDictionary)
            {
                var pattern = $@"(?<=^|[\s,;?!""']){Regex.Escape(key)}(?=[\s,;?!""']|$)";
                if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
                {
                    return key;
                }
            }

            // Check generic technical patterns
            var match = GenericTechnicalRegex.Match(content);
            if (match.Success)
            {
                return match.Value;
            }

            // Check for entities in user queries (e.g., "Tell me about Tokyo", "What is Paris?")
            var entityMatch = Regex.Match(content, @"(?:tell me about|about|info on|what is(?: the)?|capital of)\s+([A-Za-z\s]+)", RegexOptions.IgnoreCase);
            if (entityMatch.Success)
            {
                var val = entityMatch.Groups[1].Value.Trim(' ', '?', '.', '!');
                if (!string.IsNullOrWhiteSpace(val)) return val;
            }
        }

        // Pass 2: Check all recent messages
        for (int i = context.RecentMessages.Count - 1; i >= 0; i--)
        {
            var msg = context.RecentMessages[i];
            var content = msg.Content;

            foreach (var (key, _) in TechnicalDictionary)
            {
                var pattern = $@"(?<=^|[\s,;?!""']){Regex.Escape(key)}(?=[\s,;?!""']|$)";
                if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
                {
                    return key;
                }
            }

            var match = GenericTechnicalRegex.Match(content);
            if (match.Success)
            {
                return match.Value;
            }

            var weatherMatch = Regex.Match(content, @"weather in\s+([A-Za-z\s]+)", RegexOptions.IgnoreCase);
            if (weatherMatch.Success) return weatherMatch.Groups[1].Value.Trim();
        }

        return string.Empty;
    }

    private static string ResolvePronounsInText(string text, string priorSubject)
    {
        var canonicalSubject = TechnicalDictionary.TryGetValue(priorSubject, out var meta)
            ? meta.CanonicalName
            : priorSubject;

        // Resolve follow-up questions against the previously selected subject.
        var resolved = Regex.Replace(text, @"\bits creator\b", $"the creator of {canonicalSubject}", RegexOptions.IgnoreCase);
        resolved = Regex.Replace(resolved, @"\bits population\b", $"the population of {canonicalSubject}", RegexOptions.IgnoreCase);
        resolved = Regex.Replace(resolved, @"\bits key features\b", $"the key features of {canonicalSubject}", RegexOptions.IgnoreCase);
        resolved = Regex.Replace(resolved, @"\b(?:it|this|that|this language|this framework|this tool)\b", canonicalSubject, RegexOptions.IgnoreCase);

        return resolved;
    }

    private static IReadOnlyList<string> DecomposeMultiPartQuestions(string text, string? resolvedText, string primaryEntity)
    {
        var targetText = !string.IsNullOrWhiteSpace(resolvedText) ? resolvedText : text;
        var canonicalEntity = !string.IsNullOrWhiteSpace(primaryEntity)
            ? (TechnicalDictionary.TryGetValue(primaryEntity, out var meta) ? meta.CanonicalName : primaryEntity)
            : string.Empty;

        // Check if the query has compound clauses separated by commas, "and", question marks, etc.
        // E.g.: "What is C#, who created it, when was it released, and why is it popular?"
        var parts = Regex.Split(targetText, @"(?:\?+|\s*,\s*(?:and\s+)?|\s+and\s+(?:who|when|where|why|what|how)\s+)", RegexOptions.IgnoreCase)
            .Select(p => p.Trim(' ', '?', '.', ',', ';'))
            .Where(p => !string.IsNullOrWhiteSpace(p) && p.Length > 3)
            .ToList();

        if (parts.Count <= 1)
        {
            return new[] { targetText };
        }

        var result = new List<string>();
        foreach (var part in parts)
        {
            var cleanedPart = part;
            // Ensure entity context is preserved in each part
            if (!string.IsNullOrWhiteSpace(canonicalEntity) &&
                !cleanedPart.Contains(canonicalEntity, StringComparison.OrdinalIgnoreCase) &&
                !cleanedPart.Contains(primaryEntity, StringComparison.OrdinalIgnoreCase))
            {
                if (Regex.IsMatch(cleanedPart, @"^(?:who created|when was|why is|how does|what are)\b", RegexOptions.IgnoreCase))
                {
                    cleanedPart = $"{cleanedPart} {canonicalEntity}";
                }
                else if (cleanedPart.StartsWith("who", StringComparison.OrdinalIgnoreCase) ||
                         cleanedPart.StartsWith("when", StringComparison.OrdinalIgnoreCase) ||
                         cleanedPart.StartsWith("why", StringComparison.OrdinalIgnoreCase) ||
                         cleanedPart.StartsWith("what", StringComparison.OrdinalIgnoreCase))
                {
                    cleanedPart = $"{cleanedPart} for {canonicalEntity}";
                }
            }

            if (!cleanedPart.EndsWith("?"))
            {
                cleanedPart += "?";
            }

            result.Add(cleanedPart);
        }

        return result.Count > 0 ? result : new[] { targetText };
    }

    private static string DetermineIntent(string lower, bool isTechnical, bool isMultiPart)
    {
        if (isMultiPart) return "multi_part_inquiry";
        if (lower.StartsWith("what is") || lower.StartsWith("what are") || lower.StartsWith("define")) return "definition";
        if (lower.StartsWith("who is") || lower.StartsWith("who created") || lower.StartsWith("who made")) return "creator_or_identity";
        if (lower.StartsWith("when was") || lower.StartsWith("release date")) return "historical_or_date";
        if (lower.StartsWith("why is") || lower.StartsWith("why do")) return "rationale_or_popularity";
        if (lower.StartsWith("how to") || lower.StartsWith("how do") || lower.StartsWith("how does")) return "how_to";
        if (lower.StartsWith("compare") || lower.Contains(" vs ") || lower.Contains(" versus ")) return "comparison";
        if (isTechnical) return "technical_explanation";
        return "general_knowledge";
    }

    private static string ExtractTopicFallback(string prompt)
    {
        var clean = prompt
            .Replace("what is the", "", StringComparison.OrdinalIgnoreCase)
            .Replace("what is", "", StringComparison.OrdinalIgnoreCase)
            .Replace("who is", "", StringComparison.OrdinalIgnoreCase)
            .Replace("define", "", StringComparison.OrdinalIgnoreCase)
            .Replace("explain", "", StringComparison.OrdinalIgnoreCase)
            .Replace("tell me about", "", StringComparison.OrdinalIgnoreCase)
            .Trim(' ', '?', '.', '"');

        return string.IsNullOrWhiteSpace(clean) ? prompt.Trim() : clean;
    }
}
