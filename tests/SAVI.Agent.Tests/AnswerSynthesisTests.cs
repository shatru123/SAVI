using SAVI.Agent.Synthesis;
using SAVI.Agent.Understanding;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;
using Xunit;

namespace SAVI.Agent.Tests;

public class AnswerSynthesisTests
{
    private readonly AnswerSynthesisService _synthesizer = new();
    private readonly QueryUnderstandingService _analyzer = new();

    [Fact]
    public async Task Answer_IsProcessed()
    {
        var analysis = _analyzer.Analyze("What is C#?");
        var evidence = new[]
        {
            new ProviderEvidence
            {
                ProviderId = "test-provider",
                ProviderName = "Test Knowledge",
                Title = "C# Programming Language",
                Content = "C# is a modern, object-oriented language developed by Microsoft for .NET.",
                Confidence = 0.95
            }
        };

        var result = await _synthesizer.SynthesizeAsync("What is C#?", analysis, evidence, new ContextPackage(), isVoiceMode: false);

        Assert.True(result.IsVerified);
        Assert.NotEmpty(result.MainContent);
        Assert.Contains("C#", result.MainContent);
        Assert.DoesNotContain("Latin alphabet", result.MainContent);
    }

    [Fact]
    public async Task Answer_IsRelevant_RejectsLetterC()
    {
        var analysis = _analyzer.Analyze("What is C#?");
        var falseEvidence = new[]
        {
            new ProviderEvidence
            {
                ProviderId = "wiki",
                ProviderName = "Wikipedia",
                Title = "C",
                Content = "C is the third letter of the Latin alphabet.",
                Confidence = 0.92
            }
        };

        var result = await _synthesizer.SynthesizeAsync("What is C#?", analysis, falseEvidence, new ContextPackage(), isVoiceMode: false);

        // Synthesis rejects letter C and provides accurate C# programming explanation
        Assert.Contains("C#", result.MainContent);
        Assert.DoesNotContain("Latin alphabet", result.MainContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Answer_CoversAllSubQuestions()
    {
        var prompt = "What is C#, who created it, when was it released, and why is it popular?";
        var analysis = _analyzer.Analyze(prompt);

        var result = await _synthesizer.SynthesizeAsync(prompt, analysis, Array.Empty<ProviderEvidence>(), new ContextPackage(), isVoiceMode: false);

        Assert.True(result.IsComplete);
        Assert.Contains("C#", result.MainContent);
        Assert.Contains("Anders Hejlsberg", result.MainContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2000", result.MainContent);
        Assert.Contains("popular", result.MainContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RawProviderUrl_IsNotPrimaryAnswer()
    {
        var analysis = _analyzer.Analyze("What is C#?");
        var linkEvidence = new[]
        {
            new ProviderEvidence
            {
                ProviderId = "wiki",
                ProviderName = "Wikipedia",
                Title = "C# (programming language)",
                Content = "https://en.wikipedia.org/wiki/C_Sharp_(programming_language)",
                SourceUrl = "https://en.wikipedia.org/wiki/C_Sharp_(programming_language)",
                Confidence = 0.90
            }
        };

        var result = await _synthesizer.SynthesizeAsync("What is C#?", analysis, linkEvidence, new ContextPackage(), isVoiceMode: false);

        // Primary content should be natural sentences, not an isolated URL
        Assert.False(result.MainContent.Trim().StartsWith("http"));
        Assert.Contains("C#", result.MainContent);
    }

    [Fact]
    public async Task RawJson_IsNotReturnedToUser()
    {
        var analysis = _analyzer.Analyze("What is C#?");
        var jsonEvidence = new[]
        {
            new ProviderEvidence
            {
                ProviderId = "api",
                ProviderName = "Custom API",
                Title = "Result",
                Content = "{\"title\":\"C#\",\"summary\":\"A language by Microsoft\"}",
                Confidence = 0.90
            }
        };

        var result = await _synthesizer.SynthesizeAsync("What is C#?", analysis, jsonEvidence, new ContextPackage(), isVoiceMode: false);

        Assert.DoesNotContain("{\"title\"", result.MainContent);
        Assert.DoesNotContain("\"summary\":", result.MainContent);
    }

    [Fact]
    public async Task VoiceAnswer_IsConciseAndClean()
    {
        var analysis = _analyzer.Analyze("What is C#?");
        var result = await _synthesizer.SynthesizeAsync("What is C#?", analysis, Array.Empty<ProviderEvidence>(), new ContextPackage(), isVoiceMode: true);

        Assert.NotEmpty(result.VoiceContent);
        // Clean of markdown asterisks and URLs
        Assert.DoesNotContain("**", result.VoiceContent);
        Assert.DoesNotContain("`", result.VoiceContent);
        Assert.DoesNotContain("http", result.VoiceContent);
        Assert.DoesNotContain("•", result.VoiceContent);

        // Concise (sentence count between 1 and 3)
        var sentences = result.VoiceContent.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.InRange(sentences.Length, 1, 3);
    }

    [Fact]
    public async Task InsufficientEvidence_DoesNotHallucinate()
    {
        var analysis = _analyzer.Analyze("What is quantum flux in xyz-nonexistent-domain-42?");
        var result = await _synthesizer.SynthesizeAsync(
            "What is quantum flux in xyz-nonexistent-domain-42?",
            analysis,
            Array.Empty<ProviderEvidence>(),
            new ContextPackage(),
            isVoiceMode: false);

        Assert.False(result.IsVerified);
        Assert.Contains("couldn't verify", result.MainContent, StringComparison.OrdinalIgnoreCase);
    }
}
