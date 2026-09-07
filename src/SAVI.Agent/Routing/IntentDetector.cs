using System.Text.RegularExpressions;
using SAVI.Core.Constants;
using SAVI.Core.Enums;
using SAVI.Core.Models;

namespace SAVI.Agent.Routing;

public record DetectedIntent(
    string Capability,
    string Operation,
    Dictionary<string, string> Parameters,
    double Confidence,
    VerificationPolicy? PolicyOverride = null);

public class IntentDetector
{
    private static readonly Regex WeatherRegex = new(@"(?:weather|forecast|temperature|rain|climate|snow)\s+(?:in|for|at)?\s*([a-zA-Z\s]+)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CurrencyRegex = new(@"(?:convert|exchange|rate|how much is)\s+([\d\.]+)?\s*([a-zA-Z]{3})\s+(?:to|in)\s+([a-zA-Z]{3})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CryptoRegex = new(@"(?:crypto|bitcoin|\bbtc\b|ethereum|\beth\b|solana|\bsol\b|dogecoin|\bdoge\b|cardano|\bada\b|price of|crypto rate)\s*(.*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ResearchRegex = new(@"(?:papers?|research|publications?|\bdoi\b|academic studies)\s+(?:on|about|regarding)?\s*(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BookRegex = new(@"(?:books?|novel|isbn|who wrote)\s+(?:by|about|titled)?\s*(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NewsRegex = new(@"(?:hacker news|\bhn\b|tech news|developer news|technology news|startup news)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LocationRegex = new(@"(?:where is|location of|coordinates of|find place|find location)\s+([a-zA-Z\s]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EntityRegex = new(@"(?:who is the (?:prime minister|president|ceo|founder|king|queen|governor|leader)|what is the capital of)\s+([a-zA-Z\s]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex GitHubRegex = new(@"(?:github|repo|repository)\s+(?:for\s+)?([a-zA-Z0-9_\-\/]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TimeRegex = new(@"(?:what time|current time|clock|what is the date|today's date)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SystemRegex = new(@"(?:system status|specs|\bcpu\b|\bram\b|memory usage|hardware|system info|uptime|\bversion\b|\.net|\bdotnet\b|\bframework\b|os version|\bruntime\b|host info)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CalcRegex = new(@"(?:calculate|eval|compute|math|\b\d+\s*[\+\-\*\/\^]\s*\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex UnitConvertRegex = new(@"([\d\.]+)\s*(?:km|miles?|celsius|fahrenheit|c|f)\s+(?:to|in)\s+(?:km|miles?|celsius|fahrenheit|c|f)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FileListRegex = new(@"(?:list files|show files|dir|ls|browse directory)\s*(?:in|at)?\s*(.*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FileReadRegex = new(@"(?:read file|open file|view file|cat|inspect file)\s+(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FileWriteRegex = new(@"(?:create file|write file|save file)\s+([^\s]+)\s*(?:with content\s+(.+))?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FileDeleteRegex = new(@"(?:delete file|remove file|erase file|rm)\s+(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MemoryRememberRegex = new(@"(?:remember that|note that|keep in mind that|my preference is)\s+(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MemoryRecallRegex = new(@"(?:what do you remember|what are my preferences|show my memories|what do you know about me)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ListeningCheckRegex = new(@"(?:are you listening|can you hear me|can you hear|is my mic working|testing (?:mic|voice|audio|microphone)|are you there|are you online|can you understand me)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StatusCheckRegex = new(@"(?:how are you|how's it going|how are things|are you okay)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex GratitudeRegex = new(@"(?:thank you|thanks|great job|awesome|perfect|nice work)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex IdentityRegex = new(@"(?:who are you|what is your name|what can you do|introduce yourself|tell me about yourself)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex HelpRegex = new(@"(?:what (?:kind of |type of |sort of )?help (?:can you|you can) (?:provide|give)|how can you help(?: me)?|what help can you (?:provide|give)|what are your capabilities|what features do you have|what can i ask(?: you)?|^help\b|^commands\b|give me a list of commands)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CreatorRegex = new(@"(?:who (?:created|made|built|developed|programmed) (?:you|savi)|who is (?:your creator|the author|the developer|shatrughna(?:\s+ambhore)?)|creator details|developer info)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex GreetingRegex = new(@"(?:^|[\s,])(?:hi|hello|hey|greetings|good morning|good afternoon|good evening|savi)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public DetectedIntent Detect(string prompt, ContextPackage? context = null)
    {
        var clean = prompt.Trim();
        var lower = clean.ToLowerInvariant();

        // 0. Detect natural language verification override
        VerificationPolicy? policyOverride = null;
        if (lower.Contains("quick answer") || lower.Contains("fast answer") || lower.Contains("quickly"))
        {
            policyOverride = VerificationPolicy.Fast;
        }
        else if (lower.Contains("verify carefully") || lower.Contains("cross check") || lower.Contains("verified answer") || lower.Contains("cross-check"))
        {
            policyOverride = VerificationPolicy.Verified;
        }

        // 1. Voice Control Commands (Immediate)
        if (lower is "stop" or "wait" or "hold on" or "pause" or "stop speaking" or "silence")
        {
            return new DetectedIntent("voice_control", "stop", new(), 1.0, policyOverride);
        }
        if (lower is "say that again" or "repeat that" or "repeat" or "can you repeat that" or "what did you say")
        {
            return new DetectedIntent("voice_control", "repeat", new(), 1.0, policyOverride);
        }
        if (lower is "continue" or "go on" or "keep going")
        {
            return new DetectedIntent("voice_control", "continue", new(), 1.0, policyOverride);
        }

        // 2. Correction Handling: "no, I meant Pune, not Patna", "actually Pune", "I said Pune"
        var correctionMatch = Regex.Match(clean, @"^(?:no,? (?:i meant|actually)|actually,? (?:i meant)?|i said)\s+(.+)", RegexOptions.IgnoreCase);
        if (correctionMatch.Success)
        {
            var target = correctionMatch.Groups[1].Value.Trim();
            var lastWeatherMsg = context?.RecentMessages?.LastOrDefault(m => WeatherRegex.IsMatch(m.Content));
            if (lastWeatherMsg != null)
            {
                var cleanCity = Regex.Replace(target, @"(?:\s*,\s*|\s+)not\s+[a-zA-Z\s]+.*$", "", RegexOptions.IgnoreCase).Trim();
                cleanCity = cleanCity.TrimEnd(',', '.', '?', '!', ' ');
                return new DetectedIntent(SaviConstants.Capabilities.Weather, "current",
                    new Dictionary<string, string> { ["city"] = cleanCity }, 0.95, policyOverride);
            }
        }

        // 3. Conversational Follow-up Resolution (e.g. "what about tomorrow?", "and what about Mumbai?")
        if (context?.RecentMessages?.Count > 0)
        {
            var isTomorrow = lower.Contains("tomorrow") || lower.Contains("forecast") || lower.Contains("next week");
            var locationFollowup = Regex.Match(clean, @"^(?:and\s+)?(?:what about|how about|and in|and for)\s+([a-zA-Z\s]+)", RegexOptions.IgnoreCase);

            var candidate = locationFollowup.Success ? locationFollowup.Groups[1].Value.Replace("?", "").Trim() : "";
            var isTimeframeOnly = candidate.Equals("tomorrow", StringComparison.OrdinalIgnoreCase) ||
                                  candidate.Equals("today", StringComparison.OrdinalIgnoreCase) ||
                                  candidate.Equals("next week", StringComparison.OrdinalIgnoreCase);

            var lastWeatherMsg = context.RecentMessages
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefault(m => WeatherRegex.IsMatch(m.Content));

            if (lastWeatherMsg != null)
            {
                var prevWeatherMatch = WeatherRegex.Match(lastWeatherMsg.Content);
                var prevCity = prevWeatherMatch.Groups[1].Success && !string.IsNullOrWhiteSpace(prevWeatherMatch.Groups[1].Value)
                    ? prevWeatherMatch.Groups[1].Value.Trim()
                    : "Pune";

                if (isTomorrow && (isTimeframeOnly || !locationFollowup.Success))
                {
                    return new DetectedIntent(SaviConstants.Capabilities.Weather, "forecast",
                        new Dictionary<string, string> { ["city"] = prevCity, ["timeframe"] = "tomorrow" }, 0.95, policyOverride);
                }

                if (locationFollowup.Success && !isTimeframeOnly)
                {
                    var newCity = candidate;
                    return new DetectedIntent(SaviConstants.Capabilities.Weather, isTomorrow ? "forecast" : "current",
                        new Dictionary<string, string> { ["city"] = newCity, ["timeframe"] = isTomorrow ? "tomorrow" : "today" }, 0.95, policyOverride);
                }
            }

            if (lower.StartsWith("which one") || lower.StartsWith("what about the second") || lower.StartsWith("open the second") || lower.StartsWith("open it"))
            {
                var lastAssistant = context.RecentMessages.LastOrDefault(m => m.Role == MessageRole.Assistant);
                if (lastAssistant != null)
                {
                    return new DetectedIntent(SaviConstants.Capabilities.Search, "coreference_search",
                        new Dictionary<string, string>
                        {
                            ["query"] = $"{prompt} (Referencing prior context: {lastAssistant.Content[..Math.Min(100, lastAssistant.Content.Length)]})"
                        }, 0.85, policyOverride);
                }
            }
        }

        // 2. Memory commands
        var memRemMatch = MemoryRememberRegex.Match(clean);
        if (memRemMatch.Success)
        {
            return new DetectedIntent(SaviConstants.Capabilities.Memory, "remember",
                new Dictionary<string, string> { ["content"] = memRemMatch.Groups[1].Value.Trim() }, 0.95, policyOverride);
        }

        if (MemoryRecallRegex.IsMatch(clean))
        {
            return new DetectedIntent(SaviConstants.Capabilities.Memory, "recall", new Dictionary<string, string>(), 0.95, policyOverride);
        }

        // 3. Time & Date
        if (TimeRegex.IsMatch(clean))
        {
            return new DetectedIntent(SaviConstants.Capabilities.Time, "current_time", new Dictionary<string, string>(), 0.98, policyOverride);
        }

        // 4. System Diagnostics
        if (SystemRegex.IsMatch(clean))
        {
            return new DetectedIntent(SaviConstants.Capabilities.System, "diagnostics", new Dictionary<string, string>(), 0.98, policyOverride);
        }

        // 5. File Operations
        var delMatch = FileDeleteRegex.Match(clean);
        if (delMatch.Success)
        {
            return new DetectedIntent(SaviConstants.Capabilities.FileSystem, "delete",
                new Dictionary<string, string> { ["path"] = delMatch.Groups[1].Value.Trim() }, 0.95, policyOverride);
        }

        var writeMatch = FileWriteRegex.Match(clean);
        if (writeMatch.Success)
        {
            return new DetectedIntent(SaviConstants.Capabilities.FileSystem, "write",
                new Dictionary<string, string>
                {
                    ["path"] = writeMatch.Groups[1].Value.Trim(),
                    ["content"] = writeMatch.Groups[2].Success ? writeMatch.Groups[2].Value.Trim() : ""
                }, 0.95, policyOverride);
        }

        var readMatch = FileReadRegex.Match(clean);
        if (readMatch.Success)
        {
            return new DetectedIntent(SaviConstants.Capabilities.FileSystem, "read",
                new Dictionary<string, string> { ["path"] = readMatch.Groups[1].Value.Trim() }, 0.95, policyOverride);
        }

        var listMatch = FileListRegex.Match(clean);
        if (listMatch.Success)
        {
            var p = listMatch.Groups[1].Value.Trim();
            return new DetectedIntent(SaviConstants.Capabilities.FileSystem, "list",
                new Dictionary<string, string> { ["path"] = string.IsNullOrWhiteSpace(p) ? "." : p }, 0.95, policyOverride);
        }

        // 6. Weather
        var weatherMatch = WeatherRegex.Match(clean);
        if (weatherMatch.Success)
        {
            var city = weatherMatch.Groups[1].Success && !string.IsNullOrWhiteSpace(weatherMatch.Groups[1].Value)
                ? weatherMatch.Groups[1].Value.Trim()
                : "London";
            return new DetectedIntent(SaviConstants.Capabilities.Weather, "current",
                new Dictionary<string, string> { ["city"] = city }, 0.95, policyOverride);
        }

        // 7. Currency
        var currMatch = CurrencyRegex.Match(clean);
        if (currMatch.Success)
        {
            var amount = currMatch.Groups[1].Success && !string.IsNullOrWhiteSpace(currMatch.Groups[1].Value)
                ? currMatch.Groups[1].Value
                : "1";
            var from = currMatch.Groups[2].Value;
            var to = currMatch.Groups[3].Value;
            return new DetectedIntent(SaviConstants.Capabilities.Currency, "convert",
                new Dictionary<string, string> { ["amount"] = amount, ["from"] = from, ["to"] = to }, 0.95, policyOverride);
        }

        // 8. Crypto Market Rates
        var cryptoMatch = CryptoRegex.Match(clean);
        if (cryptoMatch.Success && (lower.Contains("crypto") || lower.Contains("bitcoin") || lower.Contains("btc") || lower.Contains("eth") || lower.Contains("sol") || lower.Contains("price of") || lower.Contains("rate")))
        {
            return new DetectedIntent(SaviConstants.Capabilities.Crypto, "price",
                new Dictionary<string, string> { ["coin"] = clean }, 0.95, policyOverride);
        }

        // 9. Calculator / Math / Unit conversion
        if (UnitConvertRegex.IsMatch(clean) || CalcRegex.IsMatch(clean))
        {
            return new DetectedIntent(SaviConstants.Capabilities.Calculator, "evaluate",
                new Dictionary<string, string> { ["expression"] = clean }, 0.98, policyOverride);
        }

        // 10. Research Papers / Crossref
        var researchMatch = ResearchRegex.Match(clean);
        if (researchMatch.Success)
        {
            var topic = researchMatch.Groups[1].Value.Trim();
            return new DetectedIntent(SaviConstants.Capabilities.Research, "search",
                new Dictionary<string, string> { ["query"] = topic }, 0.95, policyOverride);
        }

        // 11. Books / Open Library
        var bookMatch = BookRegex.Match(clean);
        if (bookMatch.Success && (lower.Contains("book") || lower.Contains("isbn") || lower.Contains("author") || lower.Contains("who wrote")))
        {
            var query = bookMatch.Groups[1].Value.Trim();
            return new DetectedIntent(SaviConstants.Capabilities.Books, "search",
                new Dictionary<string, string> { ["query"] = query }, 0.95, policyOverride);
        }

        // 12. Tech News / Hacker News
        if (NewsRegex.IsMatch(clean))
        {
            return new DetectedIntent(SaviConstants.Capabilities.TechNews, "top_stories",
                new Dictionary<string, string>(), 0.95, policyOverride);
        }

        // 13. Location / Coordinates / Nominatim
        var locMatch = LocationRegex.Match(clean);
        if (locMatch.Success)
        {
            var place = locMatch.Groups[1].Value.Trim();
            return new DetectedIntent(SaviConstants.Capabilities.Location, "geocode",
                new Dictionary<string, string> { ["place"] = place }, 0.95, policyOverride);
        }

        // 14. Structured Entity / Wikidata
        var entityMatch = EntityRegex.Match(clean);
        if (entityMatch.Success)
        {
            return new DetectedIntent(SaviConstants.Capabilities.Entity, "entity_lookup",
                new Dictionary<string, string> { ["entity"] = clean, ["query"] = clean }, 0.95, policyOverride);
        }

        // 15. GitHub
        var ghMatch = GitHubRegex.Match(clean);
        if (ghMatch.Success)
        {
            var repo = ghMatch.Groups[1].Value.Trim();
            return new DetectedIntent(SaviConstants.Capabilities.GitHub, "repo_info",
                new Dictionary<string, string> { ["repo"] = repo }, 0.95, policyOverride);
        }

        // 16. Chit-Chat, Listening check, Greetings & Identity
        if (ListeningCheckRegex.IsMatch(clean))
        {
            return new DetectedIntent("chitchat", "listening_check", new Dictionary<string, string>(), 0.98, policyOverride);
        }

        if (StatusCheckRegex.IsMatch(clean))
        {
            return new DetectedIntent("chitchat", "status", new Dictionary<string, string>(), 0.95, policyOverride);
        }

        if (GratitudeRegex.IsMatch(clean))
        {
            return new DetectedIntent("chitchat", "gratitude", new Dictionary<string, string>(), 0.95, policyOverride);
        }

        if (CreatorRegex.IsMatch(clean))
        {
            return new DetectedIntent("chitchat", "creator", new Dictionary<string, string>(), 0.98, policyOverride);
        }

        if (HelpRegex.IsMatch(clean) || IdentityRegex.IsMatch(clean))
        {
            return new DetectedIntent("chitchat", "identity", new Dictionary<string, string>(), 0.98, policyOverride);
        }

        if (GreetingRegex.IsMatch(clean) || lower == "hi" || lower == "hello" || lower == "hey")
        {
            return new DetectedIntent("chitchat", "greeting", new Dictionary<string, string>(), 0.95, policyOverride);
        }

        // 17. Complex reasoning vs General Knowledge
        if (lower.StartsWith("compare ") || lower.StartsWith("analyze ") || lower.StartsWith("design ") ||
            lower.StartsWith("write a function") || lower.StartsWith("write code") || lower.StartsWith("implement ") ||
            lower.StartsWith("write a program") || lower.StartsWith("refactor ") || lower.StartsWith("debug "))
        {
            return new DetectedIntent(SaviConstants.Capabilities.Reasoning, "synthesize",
                new Dictionary<string, string> { ["prompt"] = clean }, 0.90, policyOverride);
        }

        if (lower.StartsWith("who is ") || lower.StartsWith("what is ") || lower.StartsWith("define ") ||
            lower.StartsWith("explain ") || lower.StartsWith("tell me about "))
        {
            var topic = clean.Replace("who is", "", StringComparison.OrdinalIgnoreCase)
                             .Replace("what is", "", StringComparison.OrdinalIgnoreCase)
                             .Replace("define", "", StringComparison.OrdinalIgnoreCase)
                             .Replace("explain", "", StringComparison.OrdinalIgnoreCase)
                             .Trim(' ', '?', '.');

            return new DetectedIntent(SaviConstants.Capabilities.Knowledge, "summary",
                new Dictionary<string, string> { ["topic"] = topic, ["query"] = clean }, 0.90, policyOverride);
        }

        // Default: Web Search abstraction (DuckDuckGo fallback)
        return new DetectedIntent(SaviConstants.Capabilities.Search, "web_search",
            new Dictionary<string, string> { ["query"] = clean }, 0.85, policyOverride);
    }
}
