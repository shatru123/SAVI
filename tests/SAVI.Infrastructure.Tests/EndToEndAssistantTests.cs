using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using SAVI.Agent.Context;
using SAVI.Agent.Memory;
using SAVI.Agent.Orchestration;
using SAVI.Agent.Personality;
using SAVI.Agent.Planning;
using SAVI.Agent.Routing;
using SAVI.Agent.Synthesis;
using SAVI.Agent.Understanding;
using SAVI.Agent.Verification;
using SAVI.Application.DTOs;
using SAVI.Application.Interfaces;
using SAVI.Core.Entities;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;
using SAVI.Infrastructure.Caching;
using SAVI.Infrastructure.Providers;
using SAVI.Infrastructure.Providers.Calculator;
using SAVI.Infrastructure.Providers.Currency;
using SAVI.Infrastructure.Providers.Knowledge;
using SAVI.Infrastructure.Providers.System;
using SAVI.Infrastructure.Providers.Weather;
using SAVI.Infrastructure.Resilience;
using Xunit;

namespace SAVI.Infrastructure.Tests;

public class EndToEndAssistantTests
{
    private readonly IAgentOrchestrator _orchestrator;

    public EndToEndAssistantTests()
    {
        var fakeHttp = new HttpClient(new DeterministicKnowledgeHandler());
        var providers = new ICapabilityProvider[]
        {
            new SystemInfoProvider(),
            new CalculatorProvider(),
            new OpenMeteoWeatherProvider(fakeHttp),
            new FrankfurterCurrencyProvider(fakeHttp),
            new WikidataKnowledgeProvider(fakeHttp),
            new WikipediaKnowledgeProvider(fakeHttp)
        };

        var scorer = new ProviderScorer();
        var circuitBreakers = new CircuitBreakerRegistry();
        var registry = new ProviderRegistry(providers, scorer, circuitBreakers);
        var planner = new ExecutionPlanner(registry);

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var providerCache = new ProviderCache(memoryCache);

        var queryUnderstanding = new QueryUnderstandingService();
        var intentDetector = new IntentDetector(queryUnderstanding);
        var verificationEngine = new VerificationEngine();
        var personalityEngine = new PersonalityEngine();
        var answerSynthesis = new AnswerSynthesisService();
        var evidenceAggregator = new EvidenceAggregator();

        var convDto = new ConversationDetailDto { Id = "test-conv", Title = "Test Conversation" };
        var mockConvService = new Mock<IConversationService>();
        mockConvService.Setup(s => s.GetOrCreateAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(convDto);
        mockConvService.Setup(s => s.AppendMessageAsync(
            It.IsAny<string>(),
            It.IsAny<MessageRole>(),
            It.IsAny<string>(),
            It.IsAny<MessageType>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((string cId, MessageRole role, string content, MessageType type, string? t, string? s, string? m, CancellationToken ct) =>
                new Message { Id = Guid.NewGuid().ToString(), ConversationId = cId, Role = role, Content = content, MessageType = type });

        var mockContextBuilder = new Mock<IContextBuilder>();
        mockContextBuilder.Setup(b => b.BuildContextAsync(It.IsAny<AgentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextPackage
            {
                CurrentPrompt = "Test",
                RecentMessages = Array.Empty<Message>(),
                RelevantMemories = Array.Empty<MemoryItem>()
            });

        var mockMemoryService = new Mock<IMemoryService>();
        mockMemoryService.Setup(m => m.GetRelevantMemoriesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<MemoryItem>());

        var mockSettingsService = new Mock<ISettingsService>();
        mockSettingsService.Setup(s => s.GetSettingsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new UserSettingsDto());

        var mockMemoryExtractor = new Mock<IMemoryExtractor>();
        var mockToolRegistry = new Mock<IToolRegistry>();
        var mockPermissionGuard = new Mock<IPermissionGuard>();

        _orchestrator = new AgentOrchestrator(
            mockContextBuilder.Object,
            intentDetector,
            planner,
            verificationEngine,
            personalityEngine,
            mockMemoryExtractor.Object,
            mockConvService.Object,
            mockMemoryService.Object,
            mockSettingsService.Object,
            mockToolRegistry.Object,
            mockPermissionGuard.Object,
            providerCache,
            queryUnderstanding,
            answerSynthesis,
            evidenceAggregator);
    }

    [Fact]
    public async Task Query_WhatIsCSharp_ReturnsAccurateProgrammingLanguageExplanation()
    {
        var request = new AgentRequest
        {
            Message = "What is C#?",
            ConversationId = "test-conv",
            VoiceActive = false
        };

        var response = await _orchestrator.ProcessAsync(request);

        Assert.True(response.Success);
        Assert.NotEmpty(response.Message);
        Assert.Contains("C#", response.Message);
        Assert.DoesNotContain("Latin alphabet", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(response.Message.Trim().StartsWith("http"));
        Assert.DoesNotContain("{\"title\"", response.Message);
    }

    [Fact]
    public async Task Query_MultiPart_AnswersAllParts()
    {
        var request = new AgentRequest
        {
            Message = "What is C#, who created it, when was it released, and why is it popular?",
            ConversationId = "test-conv",
            VoiceActive = false,
            VerificationPolicyOverride = VerificationPolicy.Verified
        };

        var response = await _orchestrator.ProcessAsync(request);

        Assert.True(response.Success);
        Assert.Contains("C#", response.Message);
        Assert.Contains("Microsoft", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2000", response.Message);
        Assert.Contains("popular", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Query_MathExpression_RoutesToLocalCalculator()
    {
        var request = new AgentRequest
        {
            Message = "2345 * 67",
            ConversationId = "test-conv",
            VoiceActive = false
        };

        var response = await _orchestrator.ProcessAsync(request);

        Assert.True(response.Success);
        Assert.Contains("157115", response.Message);
    }

    private sealed class DeterministicKnowledgeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.ToString() ?? string.Empty;
            var body = uri.Contains("wikidata.org", StringComparison.OrdinalIgnoreCase)
                ? "{\"search\":[{\"id\":\"Q123\",\"label\":\"C#\",\"description\":\"a programming language developed by Microsoft and Anders Hejlsberg, released in 2000 and popular for building .NET applications\",\"concepturi\":\"https://www.wikidata.org/entity/Q123\"}]}"
                : "{\"title\":\"C#\",\"description\":\"programming language\",\"extract\":\"C# is a programming language developed by Microsoft and Anders Hejlsberg. It was released in 2000 and became popular because it enables developers to build .NET applications.\",\"content_urls\":{\"desktop\":{\"page\":\"https://en.wikipedia.org/wiki/C_Sharp_(programming_language)\"}}}";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
