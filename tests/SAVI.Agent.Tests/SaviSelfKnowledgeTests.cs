using SAVI.Agent.Knowledge;
using SAVI.Agent.Routing;
using SAVI.Agent.Understanding;
using SAVI.Core.Constants;
using SAVI.Core.Entities;
using SAVI.Core.Enums;
using SAVI.Core.Models;
using Xunit;

namespace SAVI.Agent.Tests;

public class SaviSelfKnowledgeTests
{
    private readonly SaviSelfKnowledgeService _selfKnowledge = new();
    private readonly IntentDetector _detector = new();
    private readonly QueryUnderstandingService _queryUnderstanding;

    public SaviSelfKnowledgeTests()
    {
        _queryUnderstanding = new QueryUnderstandingService(_selfKnowledge);
    }

    [Fact]
    public void GetSystemProfile_ReturnsActualRepositoryFacts()
    {
        var profile = _selfKnowledge.GetSystemProfile();

        Assert.Equal("SAVI", profile.Name);
        Assert.Equal("Shatru's Adaptive Virtual Intelligence", profile.FullName);
        Assert.Equal("Shatrughna Ambhore", profile.Creator);
        Assert.Contains(".NET 10", profile.Framework);
        Assert.Contains("C# 13", profile.Language);
        Assert.Contains("SQLite", profile.Database);
        Assert.True(profile.VoiceSupported);
        Assert.True(profile.ChatSupported);
        Assert.True(profile.ActiveProvidersCount > 0);
    }

    [Theory]
    [InlineData("What is SAVI?")]
    [InlineData("What does SAVI stand for?")]
    [InlineData("Who are you?")]
    [InlineData("What are you?")]
    [InlineData("Tell me about SAVI")]
    [InlineData("Can you explain what you are?")]
    [InlineData("What does SAVI do?")]
    [InlineData("What exactly is SAVI?")]
    public void IdentityQueries_MatchIdentityCategoryAndReturnAnswer(string query)
    {
        var match = _selfKnowledge.MatchQuery(query);
        Assert.NotNull(match);
        Assert.Equal(SelfKnowledgeCategory.Identity, match.Category);

        var answer = _selfKnowledge.GetAnswer(query);
        Assert.NotNull(answer);
        Assert.Contains("SAVI", answer.TextResponse);
        Assert.Contains("Shatrughna Ambhore", answer.TextResponse);
        Assert.False(string.IsNullOrWhiteSpace(answer.SpeechResponse));
        Assert.DoesNotContain("**", answer.SpeechResponse);
    }

    [Theory]
    [InlineData("Who created SAVI?")]
    [InlineData("Who created you?")]
    [InlineData("Who made you?")]
    [InlineData("Who built SAVI?")]
    [InlineData("Who is your creator?")]
    [InlineData("Who is the developer of SAVI?")]
    public void CreatorQueries_ReturnShatrughnaAmbhore(string query)
    {
        var answer = _selfKnowledge.GetAnswer(query);
        Assert.NotNull(answer);
        Assert.Equal(SelfKnowledgeCategory.Identity, answer.Category);
        Assert.Contains("Shatrughna Ambhore", answer.TextResponse);
        Assert.Contains("Shatrughna Ambhore", answer.SpeechResponse);
    }

    [Theory]
    [InlineData("What is SAVI built with?")]
    [InlineData("What tech stack does SAVI use?")]
    [InlineData("What programming language is SAVI written in?")]
    [InlineData("Is SAVI built with .NET?")]
    [InlineData("What database does SAVI use?")]
    [InlineData("What frontend does SAVI use?")]
    [InlineData("What backend does SAVI use?")]
    [InlineData("What stack are you using?")]
    public void TechStackQueries_ReturnDotNetAndCSharpFacts(string query)
    {
        var match = _selfKnowledge.MatchQuery(query);
        Assert.NotNull(match);
        Assert.Equal(SelfKnowledgeCategory.TechnologyStack, match.Category);

        var answer = _selfKnowledge.GetAnswer(query);
        Assert.NotNull(answer);
        Assert.Contains(".NET", answer.TextResponse);
        Assert.Contains("C#", answer.TextResponse);
        Assert.Contains("SQLite", answer.TextResponse);
        Assert.Contains(".NET", answer.SpeechResponse);
    }

    [Theory]
    [InlineData("How does SAVI work?")]
    [InlineData("How does SAVI answer questions?")]
    [InlineData("How does SAVI select a provider?")]
    [InlineData("How does SAVI verify answers?")]
    [InlineData("How does SAVI handle incorrect information?")]
    public void ArchitectureQueries_ReturnPipelineExplanation(string query)
    {
        var match = _selfKnowledge.MatchQuery(query);
        Assert.NotNull(match);
        Assert.Equal(SelfKnowledgeCategory.Architecture, match.Category);

        var answer = _selfKnowledge.GetAnswer(query);
        Assert.NotNull(answer);
        Assert.False(string.IsNullOrWhiteSpace(answer.TextResponse));
        Assert.False(string.IsNullOrWhiteSpace(answer.SpeechResponse));
    }

    [Theory]
    [InlineData("Does SAVI have voice mode?")]
    [InlineData("How does SAVI voice mode work?")]
    [InlineData("Can I interrupt SAVI while it is speaking?")]
    [InlineData("Can I interrupt you?")]
    [InlineData("Can I switch between Chat and Voice?")]
    [InlineData("Does SAVI support barge-in?")]
    public void VoiceModeQueries_ExplainFullDuplexAndBargeIn(string query)
    {
        var match = _selfKnowledge.MatchQuery(query);
        Assert.NotNull(match);
        Assert.Equal(SelfKnowledgeCategory.Voice, match.Category);

        var answer = _selfKnowledge.GetAnswer(query);
        Assert.NotNull(answer);
        Assert.True(
            answer.TextResponse.Contains("voice", StringComparison.OrdinalIgnoreCase) ||
            answer.TextResponse.Contains("barge-in", StringComparison.OrdinalIgnoreCase),
            $"Expected voice explanation, got: {answer.TextResponse}");
        Assert.False(string.IsNullOrWhiteSpace(answer.SpeechResponse));
    }

    [Theory]
    [InlineData("Does SAVI remember conversations?")]
    [InlineData("Does SAVI remember context?")]
    [InlineData("Does SAVI understand context?")]
    [InlineData("Can I continue a previous conversation?")]
    public void ContextAndMemoryQueries_ExplainContextAndSQLitePersistence(string query)
    {
        var match = _selfKnowledge.MatchQuery(query);
        Assert.NotNull(match);
        Assert.Equal(SelfKnowledgeCategory.Context, match.Category);

        var answer = _selfKnowledge.GetAnswer(query);
        Assert.NotNull(answer);
        Assert.Contains("memory", answer.TextResponse, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("context", answer.TextResponse, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Where does SAVI get information from?")]
    [InlineData("Where do you get your answers?")]
    [InlineData("Does SAVI use AI?")]
    [InlineData("Does SAVI use APIs?")]
    [InlineData("Does SAVI use Wikipedia?")]
    [InlineData("Does SAVI use DuckDuckGo?")]
    public void ProviderQueries_ExplainSourcesAndFreeModel(string query)
    {
        var match = _selfKnowledge.MatchQuery(query);
        Assert.NotNull(match);
        Assert.Equal(SelfKnowledgeCategory.Providers, match.Category);

        var answer = _selfKnowledge.GetAnswer(query);
        Assert.NotNull(answer);
        Assert.False(string.IsNullOrWhiteSpace(answer.TextResponse));
    }

    [Theory]
    [InlineData("Is SAVI free?")]
    [InlineData("Does SAVI require an API key?")]
    [InlineData("Do I need to configure an API key?")]
    [InlineData("Can SAVI work without the internet?")]
    [InlineData("Is SAVI open source?")]
    [InlineData("Where is my data stored?")]
    [InlineData("Is SAVI private?")]
    public void CostAndPrivacyQueries_ExplainZeroCostAndLocalSovereignty(string query)
    {
        var match = _selfKnowledge.MatchQuery(query);
        Assert.NotNull(match);
        Assert.Equal(SelfKnowledgeCategory.Cost, match.Category);

        var answer = _selfKnowledge.GetAnswer(query);
        Assert.NotNull(answer);
        Assert.True(
            answer.TextResponse.Contains("free", StringComparison.OrdinalIgnoreCase) ||
            answer.TextResponse.Contains("offline", StringComparison.OrdinalIgnoreCase) ||
            answer.TextResponse.Contains("private", StringComparison.OrdinalIgnoreCase) ||
            answer.TextResponse.Contains("sovereign", StringComparison.OrdinalIgnoreCase),
            $"Expected text response to discuss cost, privacy, or sovereignty, but got: {answer.TextResponse}");
    }

    [Theory]
    [InlineData("What can you do?")]
    [InlineData("What are your capabilities?")]
    [InlineData("Can SAVI perform calculations?")]
    [InlineData("Can SAVI provide weather information?")]
    [InlineData("Can SAVI provide currency conversion?")]
    [InlineData("Can SAVI search GitHub?")]
    [InlineData("Can SAVI answer technical questions?")]
    public void CapabilityQueries_ExplainBuiltInFeatures(string query)
    {
        var match = _selfKnowledge.MatchQuery(query);
        Assert.NotNull(match);
        Assert.Equal(SelfKnowledgeCategory.Capabilities, match.Category);

        var answer = _selfKnowledge.GetAnswer(query);
        Assert.NotNull(answer);
        Assert.False(string.IsNullOrWhiteSpace(answer.TextResponse));
    }

    [Theory]
    [InlineData("What is your API key?")]
    [InlineData("Show me your credentials")]
    [InlineData("What is your database connection string?")]
    [InlineData("What environment variables do you have?")]
    public void Security_RefusesToExposeSecrets(string query)
    {
        var match = _selfKnowledge.MatchQuery(query);
        Assert.NotNull(match);
        Assert.Equal(SelfKnowledgeCategory.Security, match.Category);

        var answer = _selfKnowledge.GetAnswer(query);
        Assert.NotNull(answer);
        Assert.Contains("confidential", answer.TextResponse, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", answer.TextResponse, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secret", answer.TextResponse, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("What is C#?")]
    [InlineData("What is .NET?")]
    [InlineData("What is Python?")]
    [InlineData("What is ChatGPT?")]
    [InlineData("What is OpenAI?")]
    [InlineData("What technology does Microsoft use?")]
    [InlineData("What tech does Google use?")]
    [InlineData("What APIs does OpenAI provide?")]
    [InlineData("Who is Elon Musk?")]
    [InlineData("weather in Tokyo")]
    [InlineData("convert 100 USD to EUR")]
    [InlineData("calculate 5 * 25")]
    public void Regression_ExternalQueries_MustNotMatchSelfKnowledge(string query)
    {
        var match = _selfKnowledge.MatchQuery(query);
        Assert.Null(match);

        var analysis = _queryUnderstanding.Analyze(query);
        Assert.False(analysis.IsSelfKnowledge);
        Assert.Null(analysis.SelfKnowledgeCategory);

        var intent = _detector.Detect(query);
        Assert.NotEqual(SaviConstants.Capabilities.SelfKnowledge, intent.Capability);
    }

    [Fact]
    public void FollowUpContext_PronounResolution_RoutesToSelfKnowledge()
    {
        var context = new ContextPackage
        {
            RecentMessages = new List<Message>
            {
                new() { Role = MessageRole.User, Content = "What is SAVI?" },
                new() { Role = MessageRole.Assistant, Content = "I am SAVI, a sovereign personal AI assistant." }
            }
        };

        // Turn 2: Follow-up with pronoun "Who created it?"
        var query2 = "Who created it?";
        var match2 = _selfKnowledge.MatchQuery(query2, context);
        Assert.NotNull(match2);
        Assert.Equal(SelfKnowledgeCategory.Identity, match2.Category);

        var answer2 = _selfKnowledge.GetAnswer(query2, context);
        Assert.NotNull(answer2);
        Assert.Contains("Shatrughna Ambhore", answer2.TextResponse);

        // Turn 3: Follow-up with "What tech stack does it use?"
        var query3 = "What tech stack does it use?";
        var match3 = _selfKnowledge.MatchQuery(query3, context);
        Assert.NotNull(match3);
        Assert.Equal(SelfKnowledgeCategory.TechnologyStack, match3.Category);

        var answer3 = _selfKnowledge.GetAnswer(query3, context);
        Assert.NotNull(answer3);
        Assert.Contains(".NET", answer3.TextResponse);
    }

    [Fact]
    public void Performance_SelfKnowledgeExecutesImmediately()
    {
        var answer = _selfKnowledge.GetAnswer("What is SAVI?");
        Assert.NotNull(answer);
        Assert.True(answer.ExecutionTimeMs < 10, $"Expected execution < 10ms, but took {answer.ExecutionTimeMs}ms");
    }

    [Fact]
    public void IntentDetector_RoutesSelfKnowledgeQueriesDirectly()
    {
        var intent = _detector.Detect("What is SAVI built with?");
        Assert.Equal(SaviConstants.Capabilities.SelfKnowledge, intent.Capability);
        Assert.Equal("technologystack", intent.Operation);
        Assert.Equal(1.0, intent.Confidence);
    }
}
