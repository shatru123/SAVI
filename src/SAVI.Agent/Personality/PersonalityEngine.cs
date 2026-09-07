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

    public string FormatFriendlyGreeting()
    {
        var greetings = new[]
        {
            "Hey Shatru! I'm online and ready. What are we working on today?",
            "Hello Shatru! Systems are all green. How can I help you out?",
            "Good to see you! SAVI core is active. What's on your mind?",
            "Ready when you are, Shatru. Let's get things done."
        };
        var idx = Random.Shared.Next(greetings.Length);
        return greetings[idx];
    }

    public string FormatChitChat(string operation, string prompt)
    {
        return operation.ToLowerInvariant() switch
        {
            "listening_check" => "Yes, Shatru! I'm listening loud and clear. My audio and reasoning systems are active. How can I help you right now?",
            "status" => "I'm doing great, Shatru! All background services and providers are running smoothly. How's everything with you?",
            "gratitude" => "You're very welcome, Shatru! Always happy to help.",
            "identity" => "I am SAVI (Shatru's Adaptive Virtual Intelligence) — your personal digital companion and intelligent task execution platform. I can check live weather, convert currencies, evaluate math, inspect files, check host system diagnostics, and remember your preferences.",
            "greeting" => FormatFriendlyGreeting(),
            _ => FormatFriendlyGreeting()
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
            return $"Sure, Shatru. Here is what I found:\n\n{content}";
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
        return content.Replace("Sure, Shatru. Here is what I found:\n\n", "")
                      .Replace("Done. ", "")
                      .Trim();
    }

    private static string CleanProfessional(string content)
    {
        return content.Replace("Hey Shatru! ", "")
                      .Replace("Done. ", "")
                      .Trim();
    }

    private static string AddWit(string content)
    {
        return $"{content}\n\n*Another triumph of synthetic intellect over entropy.*";
    }
}
