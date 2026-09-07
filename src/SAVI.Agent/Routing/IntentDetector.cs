using System.Text.RegularExpressions;
using SAVI.Core.Constants;
using SAVI.Core.Models;

namespace SAVI.Agent.Routing;

public record DetectedIntent(
    string Capability,
    string Operation,
    Dictionary<string, string> Parameters,
    double Confidence);

public class IntentDetector
{
    private static readonly Regex WeatherRegex = new(@"(?:weather|forecast|temperature|rain|climate|snow)\s+(?:in|for|at)?\s*([a-zA-Z\s]+)?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CurrencyRegex = new(@"(?:convert|exchange|rate|how much is)\s+([\d\.]+)?\s*([a-zA-Z]{3})\s+(?:to|in)\s+([a-zA-Z]{3})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
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
    private static readonly Regex CreatorRegex = new(@"(?:who (?:created|made|built|developed|programmed) (?:you|savi)|who is (?:your creator|the author|the developer|shatrughna(?:\s+ambhore)?)|creator details|developer info)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex GreetingRegex = new(@"(?:^|[\s,])(?:hi|hello|hey|greetings|good morning|good afternoon|good evening|savi)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public DetectedIntent Detect(string prompt, ContextPackage? context = null)
    {
        var clean = prompt.Trim();
        var lower = clean.ToLowerInvariant();

        // 1. Coreference / Follow-up resolution
        if (context?.RecentMessages.Count > 0)
        {
            if (lower.StartsWith("which one") || lower.StartsWith("what about the second") || lower.StartsWith("open the second") || lower.StartsWith("open it"))
            {
                var lastAssistant = context.RecentMessages.LastOrDefault(m => m.Role == Core.Enums.MessageRole.Assistant);
                if (lastAssistant != null)
                {
                    // If last was search/knowledge, route to search or knowledge
                    return new DetectedIntent(SaviConstants.Capabilities.Search, "coreference_search",
                        new Dictionary<string, string>
                        {
                            ["query"] = $"{prompt} (Referencing prior context: {lastAssistant.Content[..Math.Min(100, lastAssistant.Content.Length)]})"
                        }, 0.85);
                }
            }
        }

        // 2. Memory commands
        var memRemMatch = MemoryRememberRegex.Match(clean);
        if (memRemMatch.Success)
        {
            return new DetectedIntent(SaviConstants.Capabilities.Memory, "remember",
                new Dictionary<string, string> { ["content"] = memRemMatch.Groups[1].Value.Trim() }, 0.95);
        }

        if (MemoryRecallRegex.IsMatch(clean))
        {
            return new DetectedIntent(SaviConstants.Capabilities.Memory, "recall", new Dictionary<string, string>(), 0.95);
        }

        // 3. Time & Date
        if (TimeRegex.IsMatch(clean))
        {
            return new DetectedIntent(SaviConstants.Capabilities.Time, "current_time", new Dictionary<string, string>(), 0.98);
        }

        // 4. System Diagnostics
        if (SystemRegex.IsMatch(clean))
        {
            return new DetectedIntent(SaviConstants.Capabilities.System, "diagnostics", new Dictionary<string, string>(), 0.98);
        }

        // 5. File Operations
        var delMatch = FileDeleteRegex.Match(clean);
        if (delMatch.Success)
        {
            return new DetectedIntent(SaviConstants.Capabilities.FileSystem, "delete",
                new Dictionary<string, string> { ["path"] = delMatch.Groups[1].Value.Trim() }, 0.95);
        }

        var writeMatch = FileWriteRegex.Match(clean);
        if (writeMatch.Success)
        {
            return new DetectedIntent(SaviConstants.Capabilities.FileSystem, "write",
                new Dictionary<string, string>
                {
                    ["path"] = writeMatch.Groups[1].Value.Trim(),
                    ["content"] = writeMatch.Groups[2].Success ? writeMatch.Groups[2].Value.Trim() : ""
                }, 0.95);
        }

        var readMatch = FileReadRegex.Match(clean);
        if (readMatch.Success)
        {
            return new DetectedIntent(SaviConstants.Capabilities.FileSystem, "read",
                new Dictionary<string, string> { ["path"] = readMatch.Groups[1].Value.Trim() }, 0.95);
        }

        var listMatch = FileListRegex.Match(clean);
        if (listMatch.Success)
        {
            var p = listMatch.Groups[1].Value.Trim();
            return new DetectedIntent(SaviConstants.Capabilities.FileSystem, "list",
                new Dictionary<string, string> { ["path"] = string.IsNullOrWhiteSpace(p) ? "." : p }, 0.95);
        }

        // 6. Weather
        var weatherMatch = WeatherRegex.Match(clean);
        if (weatherMatch.Success)
        {
            var city = weatherMatch.Groups[1].Success && !string.IsNullOrWhiteSpace(weatherMatch.Groups[1].Value)
                ? weatherMatch.Groups[1].Value.Trim()
                : "London";
            return new DetectedIntent(SaviConstants.Capabilities.Weather, "current",
                new Dictionary<string, string> { ["city"] = city }, 0.90);
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
                new Dictionary<string, string> { ["amount"] = amount, ["from"] = from, ["to"] = to }, 0.95);
        }

        // 8. Calculator / Math / Unit conversion
        if (UnitConvertRegex.IsMatch(clean) || CalcRegex.IsMatch(clean))
        {
            return new DetectedIntent(SaviConstants.Capabilities.Calculator, "evaluate",
                new Dictionary<string, string> { ["expression"] = clean }, 0.90);
        }

        // 9. GitHub
        var ghMatch = GitHubRegex.Match(clean);
        if (ghMatch.Success)
        {
            var repo = ghMatch.Groups[1].Value.Trim();
            return new DetectedIntent(SaviConstants.Capabilities.GitHub, "repo_info",
                new Dictionary<string, string> { ["repo"] = repo }, 0.90);
        }

        // 10. Chit-Chat, Listening check, Greetings & Identity
        if (ListeningCheckRegex.IsMatch(clean))
        {
            return new DetectedIntent("chitchat", "listening_check", new Dictionary<string, string>(), 0.98);
        }

        if (StatusCheckRegex.IsMatch(clean))
        {
            return new DetectedIntent("chitchat", "status", new Dictionary<string, string>(), 0.95);
        }

        if (GratitudeRegex.IsMatch(clean))
        {
            return new DetectedIntent("chitchat", "gratitude", new Dictionary<string, string>(), 0.95);
        }

        if (CreatorRegex.IsMatch(clean))
        {
            return new DetectedIntent("chitchat", "creator", new Dictionary<string, string>(), 0.98);
        }

        if (IdentityRegex.IsMatch(clean))
        {
            return new DetectedIntent("chitchat", "identity", new Dictionary<string, string>(), 0.95);
        }

        if (GreetingRegex.IsMatch(clean) || lower == "hi" || lower == "hello" || lower == "hey")
        {
            return new DetectedIntent("chitchat", "greeting", new Dictionary<string, string>(), 0.95);
        }

        // 11. Knowledge & Reasoning vs Web Search
        if (lower.StartsWith("who is ") || lower.StartsWith("what is ") || lower.StartsWith("define ") ||
            lower.StartsWith("explain ") || lower.StartsWith("why ") || lower.StartsWith("how ") ||
            lower.StartsWith("write ") || lower.StartsWith("code ") || lower.StartsWith("generate ") ||
            lower.StartsWith("can you ") || lower.StartsWith("tell me ") || lower.StartsWith("solve "))
        {
            var topic = clean.Replace("who is", "", StringComparison.OrdinalIgnoreCase)
                             .Replace("what is", "", StringComparison.OrdinalIgnoreCase)
                             .Replace("define", "", StringComparison.OrdinalIgnoreCase)
                             .Replace("explain", "", StringComparison.OrdinalIgnoreCase)
                             .Trim(' ', '?', '.');

            return new DetectedIntent(SaviConstants.Capabilities.Knowledge, "summary",
                new Dictionary<string, string> { ["topic"] = topic, ["query"] = clean }, 0.92);
        }

        // Default: Search provider (handled by FreeAiProvider primary, DuckDuckGo fallback)
        return new DetectedIntent(SaviConstants.Capabilities.Search, "web_search",
            new Dictionary<string, string> { ["query"] = clean }, 0.85);
    }
}
