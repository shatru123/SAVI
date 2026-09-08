using SAVI.Agent.Understanding;
using SAVI.Core.Entities;
using SAVI.Core.Enums;
using SAVI.Core.Models;
using Xunit;

namespace SAVI.Agent.Tests;

public class QueryUnderstandingTests
{
    private readonly QueryUnderstandingService _service = new();

    [Fact]
    public void CSharp_IsRecognizedAsTechnicalEntity()
    {
        var result = _service.Analyze("What is C#?");

        Assert.True(result.IsTechnical);
        Assert.Contains("C#", result.Entities, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Programming", result.Domain);
        Assert.Contains("C# programming language", result.CanonicalLookupQuery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CSharp_DoesNotResolveToLetterC()
    {
        var result = _service.Analyze("What is C#?");

        Assert.DoesNotContain(result.Entities, e => e.Equals("C", StringComparison.OrdinalIgnoreCase));
        Assert.False(result.Topic.Equals("C", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("C#", result.CanonicalLookupQuery);
    }

    [Fact]
    public void Cpp_IsRecognizedCorrectly()
    {
        var result = _service.Analyze("Explain C++ memory management");

        Assert.True(result.IsTechnical);
        Assert.Contains("C++", result.Entities, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Programming", result.Domain);
        Assert.Contains("C++ programming language", result.CanonicalLookupQuery);
    }

    [Fact]
    public void DotNet_IsRecognizedCorrectly()
    {
        var result = _service.Analyze("Tell me about .NET 10 features");

        Assert.True(result.IsTechnical);
        Assert.Contains(result.Entities, e => e.Contains(".NET", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Programming", result.Domain);
    }

    [Fact]
    public void AspNetCore_IsRecognizedCorrectly()
    {
        var result = _service.Analyze("How does middleware work in ASP.NET Core?");

        Assert.True(result.IsTechnical);
        Assert.Contains(result.Entities, e => e.Contains("ASP.NET Core", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Programming", result.Domain);
    }

    [Theory]
    [InlineData("How to use IEnumerable<T> in C#?", "IEnumerable<T>")]
    [InlineData("Understanding Task<T> asynchronous methods", "Task<T>")]
    [InlineData("Configure OAuth 2.0 authentication", "OAuth 2.0")]
    [InlineData("Verify JWT token signature", "JWT")]
    [InlineData("Is AES-128 secure?", "AES-128")]
    [InlineData("Explain HTTP/2 multiplexing", "HTTP/2")]
    public void TechnicalSyntax_IsPreserved(string prompt, string expectedSymbol)
    {
        var result = _service.Analyze(prompt);

        Assert.True(result.IsTechnical);
        Assert.True(result.Entities.Any(e => e.Equals(expectedSymbol, StringComparison.OrdinalIgnoreCase)) ||
                    result.RawPrompt.Contains(expectedSymbol));
    }

    [Fact]
    public void FollowUp_UsesConversationContext()
    {
        var context = new ContextPackage
        {
            RecentMessages = new List<Message>
            {
                new() { Role = MessageRole.User, Content = "What is C#?" },
                new() { Role = MessageRole.Assistant, Content = "C# is a modern, object-oriented language for .NET." }
            }
        };

        var result = _service.Analyze("Who created it?", context);

        Assert.NotNull(result.ResolvedContextQuery);
        Assert.Contains("C#", result.ResolvedContextQuery, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.IsTechnical);
    }

    [Fact]
    public void PronounResolution_UsesPreviousEntity()
    {
        var context = new ContextPackage
        {
            RecentMessages = new List<Message>
            {
                new() { Role = MessageRole.User, Content = "Tell me about Tokyo" },
                new() { Role = MessageRole.Assistant, Content = "Tokyo is the capital of Japan." }
            }
        };

        var result = _service.Analyze("What is its population?", context);

        Assert.NotNull(result.ResolvedContextQuery);
        Assert.Contains("Tokyo", result.ResolvedContextQuery, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MultiPartQuestion_DecomposesIntoSubQuestions()
    {
        var prompt = "What is C#, who created it, when was it released, and why is it popular?";
        var result = _service.Analyze(prompt);

        Assert.True(result.SubQuestions.Count >= 3, $"Expected >= 3 sub-questions, but got {result.SubQuestions.Count}");
        Assert.Contains(result.SubQuestions, q => q.Contains("C#", StringComparison.OrdinalIgnoreCase));
    }
}
