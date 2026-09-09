using SAVI.Agent.Routing;
using SAVI.Core.Constants;
using SAVI.Core.Entities;
using SAVI.Core.Models;
using Xunit;

namespace SAVI.Agent.Tests;

public class IntentDetectorTests
{
    private readonly IntentDetector _detector = new();

    [Fact]
    public void Detect_WeatherQuery_ShouldRouteToWeatherCapability()
    {
        var intent = _detector.Detect("What is the weather in Paris today?");
        Assert.Equal(SaviConstants.Capabilities.Weather, intent.Capability);
        Assert.Equal("current", intent.Operation);
        Assert.True(intent.Parameters.ContainsKey("city"));
    }

    [Fact]
    public void Detect_CurrencyQuery_ShouldRouteToCurrencyCapability()
    {
        var intent = _detector.Detect("Convert 150 USD to EUR");
        Assert.Equal(SaviConstants.Capabilities.Currency, intent.Capability);
        Assert.Equal("convert", intent.Operation);
        Assert.Equal("150", intent.Parameters["amount"]);
        Assert.Equal("USD", intent.Parameters["from"]);
        Assert.Equal("EUR", intent.Parameters["to"]);
    }

    [Fact]
    public void Detect_MathExpression_ShouldRouteToCalculator()
    {
        var intent = _detector.Detect("Calculate 45 * (12 + 8)");
        Assert.Equal(SaviConstants.Capabilities.Calculator, intent.Capability);
        Assert.Equal("evaluate", intent.Operation);
    }

    [Fact]
    public void Detect_TimeQuery_ShouldRouteToTimeCapability()
    {
        var intent = _detector.Detect("What time is it right now?");
        Assert.Equal(SaviConstants.Capabilities.Time, intent.Capability);
    }

    [Fact]
    public void Detect_SystemQuery_ShouldRouteToSystemCapability()
    {
        var intent = _detector.Detect("Show system status and RAM usage");
        Assert.Equal(SaviConstants.Capabilities.System, intent.Capability);
    }

    [Fact]
    public void Detect_DotNetKnowledgeQuery_ShouldNotRouteToSystemDiagnostics()
    {
        var intent = _detector.Detect("What is .NET?");
        Assert.Equal(SaviConstants.Capabilities.Knowledge, intent.Capability);
    }

    [Theory]
    [InlineData("Who is the current president of India?")]
    [InlineData("What is the latest GitHub release?")]
    [InlineData("What happened in the markets today?")]
    public void Detect_CurrentInformation_ShouldRouteToLiveSearch(string prompt)
    {
        var intent = _detector.Detect(prompt);
        Assert.Equal(SaviConstants.Capabilities.Search, intent.Capability);
        Assert.Equal("web_search", intent.Operation);
    }

    [Fact]
    public void Detect_MemoryRememberQuery_ShouldRouteToMemory()
    {
        var intent = _detector.Detect("Remember that I prefer dark theme and concise explanations");
        Assert.Equal(SaviConstants.Capabilities.Memory, intent.Capability);
        Assert.Equal("remember", intent.Operation);
        Assert.Contains("dark theme", intent.Parameters["content"]);
    }

    [Fact]
    public void Detect_Coreference_ShouldResolveToPriorContext()
    {
        var context = new ContextPackage
        {
            RecentMessages = new List<Message>
            {
                new() { Role = Core.Enums.MessageRole.Assistant, Content = "1. .NET in Action 2. C# Yellow Book 3. Pro .NET 10" }
            }
        };

        var intent = _detector.Detect("Which one is cheapest?", context);
        Assert.Equal(SaviConstants.Capabilities.Search, intent.Capability);
        Assert.Equal("coreference_search", intent.Operation);
    }

    [Theory]
    [InlineData("What kind of help you can provide?")]
    [InlineData("How can you help me?")]
    [InlineData("What are your capabilities?")]
    [InlineData("What help can you provide?")]
    [InlineData("What can you do?")]
    public void Detect_HelpAndCapabilitiesQueries_ShouldRouteToChitchatIdentity(string prompt)
    {
        var intent = _detector.Detect(prompt);
        Assert.Equal("chitchat", intent.Capability);
        Assert.Equal("identity", intent.Operation);
        Assert.True(intent.Confidence >= 0.95);
    }
}
