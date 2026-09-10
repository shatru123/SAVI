using SAVI.Core.Constants;
using SAVI.Core.Entities;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;

namespace SAVI.Agent.Personality;

public class PersonalityEngine : IPersonalityEngine
{
    public string FormatResponse(
        string rawContent,
        PersonalityMode mode,
        IReadOnlyList<MemoryItem> relevantMemories,
        bool isVoice = false)
    {
        var clean = rawContent.Trim();
        var prefersConcise = relevantMemories.Any(m => m.Content.Contains("concise", StringComparison.OrdinalIgnoreCase));

        if (prefersConcise && mode == PersonalityMode.Friendly)
        {
            mode = PersonalityMode.Concise;
        }

        return mode switch
        {
            PersonalityMode.Concise => CleanConcise(clean),
            PersonalityMode.Professional => CleanProfessional(clean),
            PersonalityMode.Calm => $"Understood. {clean}",
            PersonalityMode.Witty => AddWit(clean),
            PersonalityMode.Motivational => $"You've got this! {clean}",
            PersonalityMode.Friendly => MakeFriendly(clean, isVoice),
            _ => MakeFriendly(clean, isVoice)
        };
    }

    public string FormatFriendlyGreeting(string? userName = null)
    {
        var name = string.IsNullOrWhiteSpace(userName) ? SaviConstants.DefaultUser : userName.Trim();
        var greetings = new[]
        {
            $"Hey {name}! I'm online and ready. What are we working on today?",
            $"Hello {name}! Systems are all green. How can I help you out?",
            $"Good to see you, {name}! SAVI core is active. What's on your mind?",
            $"Ready when you are, {name}. Let's get things done."
        };
        var idx = Random.Shared.Next(greetings.Length);
        return greetings[idx];
    }

    public string FormatChitChat(string operation, string prompt, string? userName = null)
    {
        var name = string.IsNullOrWhiteSpace(userName) ? "there" : userName.Trim();
        return operation.ToLowerInvariant() switch
        {
            "check_prompt" => "Sure, what do you need?",
            "never_mind" => "No worries.",
            "wait" => "Yep?",
            "go_back" => "Sure.",
            "clarify" => "Got it. What did you mean?",
            "listening_check" => $"Yes, {name}! I'm listening loud and clear. My audio and reasoning systems are active. How can I help you right now?",
            "status" => $"I'm doing great, {name}! All background services and providers are running smoothly. How's everything with you?",
            "gratitude" => $"You're very welcome, {name}! Always happy to help.",
            "identity" => "I am SAVI (Shatru's Adaptive Virtual Intelligence) — an advanced personal digital assistant and autonomous task execution platform created by Shatrughna Ambhore.\n\nHere is how I can assist you:\n\n• 💻 Coding & Development: Write, debug, and explain algorithms and code in C#, Python, JavaScript, TypeScript, Go, SQL, and more.\n• ⚡ Agentic Tasks: Autonomous multi-step planning, solution generation, and cross-verification.\n• 🌦️ Real-Time Weather: Live forecasts, temperature, and conditions worldwide (e.g. \"weather in Tokyo\").\n• 💱 Currency Conversion: Live FX rates across global currencies (e.g. \"convert 100 USD to INR\").\n• 🔍 Knowledge & Search: Detailed answers, topic research, and factual summaries.\n• 🔢 Math & Computation: Calculations, mathematical formulas, and unit conversions.\n• 📁 Host Diagnostics & Files: System specs, CPU/RAM stats, directory inspection, and file operations.\n• 💾 Persistent Memory: Remembers your preferences across conversations (e.g. \"Remember that I prefer C#\").\n• 🔊 Voice Talk-Back: Speech recognition and real-time voice response (toggle ON/OFF anytime in the top bar).\n\nWhat would you like to work on today?",
            "creator" => "I was created and architected by Shatrughna Ambhore. SAVI (Shatru's Adaptive Virtual Intelligence) is built as a personal digital companion and sovereign task execution platform.",
            "greeting" => FormatFriendlyGreeting(userName),
            _ => FormatFriendlyGreeting(userName)
        };
    }

    public string FormatError(string reason)
    {
        return $"I ran into a problem while checking that: {reason}. I've noted this in the diagnostics log.";
    }

    public string FormatActionApprovalPrompt(string actionDescription, PermissionLevel level)
    {
        return $"I can do that, but {actionDescription} is classified as a {level.ToString().ToUpperInvariant()} action. Would you like me to proceed?";
    }

    private static string MakeFriendly(string content, bool isVoice)
    {
        if (content.StartsWith("•") || content.Contains('\n'))
        {
            return $"Sure. Here is what I found:\n\n{content}";
        }

        // Do not prepend "Done." to complete natural conversational sentences
        if (content.StartsWith("I ", StringComparison.OrdinalIgnoreCase) ||
            content.StartsWith("Yes", StringComparison.OrdinalIgnoreCase) ||
            content.StartsWith("Here", StringComparison.OrdinalIgnoreCase) ||
            content.StartsWith("Got it", StringComparison.OrdinalIgnoreCase) ||
            content.StartsWith("Understood", StringComparison.OrdinalIgnoreCase) ||
            content.StartsWith("Hello", StringComparison.OrdinalIgnoreCase) ||
            content.StartsWith("Hey", StringComparison.OrdinalIgnoreCase) ||
            content.StartsWith("Sure", StringComparison.OrdinalIgnoreCase) ||
            content.StartsWith("Done", StringComparison.OrdinalIgnoreCase))
        {
            return content;
        }

        return $"Sure, {content}";
    }

    private static string CleanConcise(string content)
    {
        return content.Replace("Sure. Here is what I found:\n\n", "")
                      .Replace("Sure, Shatru. Here is what I found:\n\n", "")
                      .Replace("Done. ", "")
                      .Trim();
    }

    private static string CleanProfessional(string content)
    {
        return content.Replace("Hey Shatru! ", "")
                      .Replace("Hey! ", "")
                      .Replace("Done. ", "")
                      .Trim();
    }

    private static string AddWit(string content)
    {
        return $"{content}\n\n*Another triumph of synthetic intellect over entropy.*";
    }
}
