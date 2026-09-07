using System.Text.RegularExpressions;
using SAVI.Core.Interfaces;

namespace SAVI.Infrastructure.Voice;

public class VoiceResponseFormatter : IVoiceResponseFormatter
{
    private static readonly Regex CodeBlockRegex = new(@"```[\s\S]*?```", RegexOptions.Compiled);
    private static readonly Regex InlineCodeRegex = new(@"`[^`]*`", RegexOptions.Compiled);
    private static readonly Regex MarkdownTableRegex = new(@"\|[^\n]+\|\r?\n\|[-:\s|]+\|\r?\n(?:\|[^\n]+\|\r?\n?)*", RegexOptions.Compiled);
    private static readonly Regex UrlRegex = new(@"https?:\/\/[^\s\)\>]+", RegexOptions.Compiled);
    private static readonly Regex MarkdownLinkRegex = new(@"\[([^\]]+)\]\([^\)]+\)", RegexOptions.Compiled);
    private static readonly Regex CitationBadgeRegex = new(@"(?:✓|✔)\s*[^(\n]+\(\d+%\)", RegexOptions.Compiled);
    private static readonly Regex BulletsRegex = new(@"^[\s*•\-]+", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex HeadersRegex = new(@"^#{1,6}\s*", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex FormattingCharsRegex = new(@"[*_~>#]", RegexOptions.Compiled);
    private static readonly Regex MultipleSpacesRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex SentenceSplitRegex = new(@"[^.!?]+[.!?]+|[^.!?]+$", RegexOptions.Compiled);

    public string FormatForSpeech(string rawResponse, string? capability = null)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return "I'm standing by, Shatru.";
        }

        var text = rawResponse.Trim();

        // 1. Replace code blocks with a natural spoken prompt
        text = CodeBlockRegex.Replace(text, " The code solution is displayed on screen. ");

        // 2. Remove markdown tables and replace with conversational prompt
        text = MarkdownTableRegex.Replace(text, " The structured data table is displayed on your screen. ");

        // 3. Remove inline code backticks
        text = InlineCodeRegex.Replace(text, m => m.Value.Replace("`", ""));

        // 4. Simplify markdown links to just their label
        text = MarkdownLinkRegex.Replace(text, "$1");

        // 5. Remove citations and verification badges
        text = CitationBadgeRegex.Replace(text, "");

        // 6. Remove raw URLs
        text = UrlRegex.Replace(text, "");

        // 7. Clean bullets and headers
        text = BulletsRegex.Replace(text, "");
        text = HeadersRegex.Replace(text, "");

        // 8. Clean leftover markdown chars
        text = FormattingCharsRegex.Replace(text, "");

        // 9. Normalize whitespace and newlines
        text = text.Replace("\r\n", " ").Replace("\n", " ");
        text = MultipleSpacesRegex.Replace(text, " ").Trim();

        // 10. Natural conversational polish (friend-like)
        text = ApplyConversationalTone(text, capability);

        return text;
    }

    public IReadOnlyList<string> ChunkForStreamingTts(string speechText)
    {
        if (string.IsNullOrWhiteSpace(speechText))
        {
            return Array.Empty<string>();
        }

        var matches = SentenceSplitRegex.Matches(speechText);
        var chunks = new List<string>();

        foreach (Match match in matches)
        {
            var chunk = match.Value.Trim();
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                chunks.Add(chunk);
            }
        }

        if (chunks.Count == 0 && !string.IsNullOrWhiteSpace(speechText))
        {
            chunks.Add(speechText.Trim());
        }

        return chunks;
    }

    private static string ApplyConversationalTone(string text, string? capability)
    {
        // Convert dry technical phrases to friend-like speech
        if (text.StartsWith("The weather in", StringComparison.OrdinalIgnoreCase))
        {
            text = "Right now, " + char.ToLowerInvariant(text[0]) + text[1..];
        }

        if (text.StartsWith("Provider status is healthy", StringComparison.OrdinalIgnoreCase))
        {
            text = "All neural systems are online and running smoothly.";
        }

        // Strip robotic metadata prefixes
        if (text.StartsWith("Answer:", StringComparison.OrdinalIgnoreCase))
        {
            text = text[7..].Trim();
        }

        // Limit excessively long speech chunks to ~350 characters for voice brevity unless asked
        if (text.Length > 350 && text.Contains('.'))
        {
            var periodIndex = text.IndexOf('.', 200);
            if (periodIndex > 0 && periodIndex < text.Length - 1)
            {
                text = text[..(periodIndex + 1)] + " I've provided the full details on your screen.";
            }
        }

        return text;
    }
}
