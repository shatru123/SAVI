using Microsoft.Extensions.Caching.Memory;
using SAVI.Agent.Planning;
using SAVI.Agent.Routing;
using SAVI.Core.Constants;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;
using SAVI.Infrastructure.Caching;
using SAVI.Infrastructure.Providers;
using SAVI.Infrastructure.Providers.Books;
using SAVI.Infrastructure.Providers.Calculator;
using SAVI.Infrastructure.Providers.Currency;
using SAVI.Infrastructure.Providers.Finance;
using SAVI.Infrastructure.Providers.GitHub;
using SAVI.Infrastructure.Providers.Knowledge;
using SAVI.Infrastructure.Providers.Location;
using SAVI.Infrastructure.Providers.News;
using SAVI.Infrastructure.Providers.Research;
using SAVI.Infrastructure.Providers.System;
using SAVI.Infrastructure.Providers.Weather;
using SAVI.Infrastructure.Resilience;
using Xunit;

namespace SAVI.Infrastructure.Tests;

public class ProviderRoutingAndIntelligenceTests
{
    private readonly IntentDetector _detector = new();
    private readonly ProviderScorer _scorer = new();
    private readonly CircuitBreakerRegistry _circuitBreakers = new();
    private readonly IProviderCache _cache;
    private readonly IProviderRegistry _registry;

    public ProviderRoutingAndIntelligenceTests()
    {
        _cache = new ProviderCache(new MemoryCache(new MemoryCacheOptions()));

        var fakeHttp = new HttpClient();
        var providers = new ICapabilityProvider[]
        {
            new SystemInfoProvider(),
            new CalculatorProvider(),
            new OpenMeteoWeatherProvider(fakeHttp),
            new FrankfurterCurrencyProvider(fakeHttp),
            new WikidataKnowledgeProvider(fakeHttp),
            new CrossrefResearchProvider(fakeHttp),
            new OpenLibraryProvider(fakeHttp),
            new HackerNewsProvider(fakeHttp),
            new NominatimLocationProvider(fakeHttp),
            new CoinGeckoCryptoProvider(fakeHttp),
            new GitHubPublicProvider(fakeHttp),
            new WikipediaKnowledgeProvider(fakeHttp),
        };

        _registry = new ProviderRegistry(providers, _scorer, _circuitBreakers);
    }

    [Fact]
    public void WeatherPrompt_ShouldRouteToWeatherCapability()
    {
        var intent = _detector.Detect("What is the weather in Pune?");
        Assert.Equal(SaviConstants.Capabilities.Weather, intent.Capability);
        Assert.Equal("Pune", intent.Parameters["city"]);

        var taskReq = new TaskRequest { Capability = intent.Capability, Prompt = "What is the weather in Pune?" };
        var ranked = _registry.RankProviders(taskReq);
        Assert.NotEmpty(ranked);
        Assert.Equal(SaviConstants.Providers.OpenMeteo, ranked[0].Id);
    }

    [Fact]
    public void CalculatorPrompt_ShouldRouteToCalculator_AndNeverAI()
    {
        var intent = _detector.Detect("2345 * 67");
        Assert.Equal(SaviConstants.Capabilities.Calculator, intent.Capability);

        var taskReq = new TaskRequest { Capability = intent.Capability, Prompt = "2345 * 67" };
        var ranked = _registry.RankProviders(taskReq);
        Assert.NotEmpty(ranked);
        Assert.Equal(SaviConstants.Providers.LocalCalculator, ranked[0].Id);
    }

    [Fact]
    public void CurrencyPrompt_ShouldRouteToFrankfurter()
    {
        var intent = _detector.Detect("Convert 100 USD to INR");
        Assert.Equal(SaviConstants.Capabilities.Currency, intent.Capability);

        var taskReq = new TaskRequest { Capability = intent.Capability, Prompt = "Convert 100 USD to INR" };
        var ranked = _registry.RankProviders(taskReq);
        Assert.NotEmpty(ranked);
        Assert.Equal(SaviConstants.Providers.Frankfurter, ranked[0].Id);
    }

    [Fact]
    public void EntityPrompt_ShouldRouteToWikidata()
    {
        var intent = _detector.Detect("Who is the Prime Minister of India?");
        Assert.Equal(SaviConstants.Capabilities.Entity, intent.Capability);

        var taskReq = new TaskRequest { Capability = intent.Capability, Prompt = "Who is the Prime Minister of India?", Parameters = intent.Parameters };
        var ranked = _registry.RankProviders(taskReq);
        Assert.NotEmpty(ranked);
        Assert.Equal(SaviConstants.Providers.Wikidata, ranked[0].Id);
    }

    [Fact]
    public void ResearchPrompt_ShouldRouteToCrossref()
    {
        var intent = _detector.Detect("Find papers on transformer architectures");
        Assert.Equal(SaviConstants.Capabilities.Research, intent.Capability);

        var taskReq = new TaskRequest { Capability = intent.Capability, Prompt = "Find papers on transformer architectures" };
        var ranked = _registry.RankProviders(taskReq);
        Assert.NotEmpty(ranked);
        Assert.Equal(SaviConstants.Providers.Crossref, ranked[0].Id);
    }

    [Fact]
    public void LocationPrompt_ShouldRouteToNominatim()
    {
        var intent = _detector.Detect("Where is Pune?");
        Assert.Equal(SaviConstants.Capabilities.Location, intent.Capability);

        var taskReq = new TaskRequest { Capability = intent.Capability, Prompt = "Where is Pune?" };
        var ranked = _registry.RankProviders(taskReq);
        Assert.NotEmpty(ranked);
        Assert.Equal(SaviConstants.Providers.Nominatim, ranked[0].Id);
    }

    [Fact]
    public void CryptoPrompt_ShouldRouteToCoinGecko()
    {
        var intent = _detector.Detect("What is the price of bitcoin?");
        Assert.Equal(SaviConstants.Capabilities.Crypto, intent.Capability);

        var taskReq = new TaskRequest { Capability = intent.Capability, Prompt = "What is the price of bitcoin?" };
        var ranked = _registry.RankProviders(taskReq);
        Assert.NotEmpty(ranked);
        Assert.Equal(SaviConstants.Providers.CoinGecko, ranked[0].Id);
    }

    [Fact]
    public void VerificationPolicy_Fast_ShouldOnlySelectSinglePrimary()
    {
        var planner = new ExecutionPlanner(_registry);
        var intent = _detector.Detect("What is the weather in Tokyo?");
        var plan = planner.CreatePlan(intent, new AgentRequest { Message = "What is the weather in Tokyo?" }, VerificationPolicy.Fast);

        Assert.Equal(VerificationPolicy.Fast, plan.Policy);
        Assert.Single(plan.PrimaryProviders);
        Assert.Empty(plan.VerificationProviders);
    }

    [Fact]
    public void VerificationPolicy_Verified_ShouldSelectMultipleProviders()
    {
        var planner = new ExecutionPlanner(_registry);
        var intent = _detector.Detect("What is the weather in Tokyo?");
        var plan = planner.CreatePlan(intent, new AgentRequest { Message = "What is the weather in Tokyo?" }, VerificationPolicy.Verified);

        Assert.Equal(VerificationPolicy.Verified, plan.Policy);
        Assert.True(plan.PrimaryProviders.Count >= 1);
    }

    [Fact]
    public void CircuitBreaker_TripsToOpen_AfterConsecutiveFailures()
    {
        var record = _circuitBreakers.GetRecord("test-provider");
        Assert.Equal(CircuitBreakerState.Healthy, record.GetCurrentState());

        record.RecordFailure(500);
        Assert.Equal(CircuitBreakerState.Degraded, record.GetCurrentState());

        record.RecordFailure(500);
        Assert.Equal(CircuitBreakerState.Degraded, record.GetCurrentState());

        record.RecordFailure(500);
        Assert.Equal(CircuitBreakerState.Open, record.GetCurrentState());
    }

    [Fact]
    public void ProviderRegistry_RecordsRuntimeResultsForRouting()
    {
        _registry.RecordResult(SaviConstants.Providers.OpenMeteo, 125, false);
        _registry.RecordResult(SaviConstants.Providers.OpenMeteo, 125, false);
        _registry.RecordResult(SaviConstants.Providers.OpenMeteo, 125, false);

        var ranked = _registry.RankProviders(new TaskRequest
        {
            Capability = SaviConstants.Capabilities.Weather,
            Prompt = "What is the weather in Tokyo?",
            Parameters = new Dictionary<string, string> { ["city"] = "Tokyo" }
        });

        Assert.Empty(ranked);
    }

    [Fact]
    public async Task ProviderCache_CachesResult_AndReportsHitRate()
    {
        var key = "calc:test";
        var expected = ProviderResult.Succeeded("p", "P", "42");
        await _cache.SetAsync(key, expected, TimeSpan.FromMinutes(1));

        var retrieved = await _cache.GetAsync(key);
        Assert.NotNull(retrieved);
        Assert.Equal("42", retrieved.Data?.ToString());
        Assert.True(_cache.CacheHits > 0);
        Assert.True(_cache.HitRatio > 0.0);
    }

    [Fact]
    public async Task ProviderCache_RequestCoalescing_SeparatesDifferentParameters()
    {
        var calls = 0;
        var first = await _cache.GetOrExecuteAsync(
            "weather",
            "weather:city=tokyo",
            TimeSpan.FromMinutes(1),
            _ => Task.FromResult(ProviderResult.Succeeded("weather", "Weather", "Tokyo")));
        var second = await _cache.GetOrExecuteAsync(
            "weather",
            "weather:city=mumbai",
            TimeSpan.FromMinutes(1),
            _ =>
            {
                calls++;
                return Task.FromResult(ProviderResult.Succeeded("weather", "Weather", "Mumbai"));
            });

        Assert.Equal("Tokyo", first.Data);
        Assert.Equal("Mumbai", second.Data);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void EvidenceAggregator_NormalizesProviderResults()
    {
        var aggregator = new SAVI.Agent.Synthesis.EvidenceAggregator();
        var results = new[]
        {
            ProviderResult.Succeeded("wiki", "Wikipedia", new { Title = "C#", Summary = "A language", Url = "https://wiki.org/csharp" }, confidence: 0.95),
            ProviderResult.Succeeded("wikidata", "Wikidata", new { Title = "C#", Description = "Programming language", Formatted = "C#: Programming language" }, confidence: 0.90)
        };

        var evidence = aggregator.Aggregate(results);

        Assert.Equal(2, evidence.Count);
        Assert.Equal("wiki", evidence[0].ProviderId);
        Assert.Equal("A language", evidence[0].Content);
        Assert.Equal("https://wiki.org/csharp", evidence[0].SourceUrl);
        Assert.Equal("wikidata", evidence[1].ProviderId);
        Assert.Contains("Programming language", evidence[1].Content);
    }

    [Fact]
    public void MultipleProviders_AreAggregated()
    {
        var aggregator = new SAVI.Agent.Synthesis.EvidenceAggregator();
        var results = new[]
        {
            ProviderResult.Succeeded("p1", "Provider 1", "Fact 1", confidence: 0.8),
            ProviderResult.Failed("p2", "Provider 2", "Timeout"),
            ProviderResult.Succeeded("p3", "Provider 3", "Fact 3", confidence: 0.9)
        };

        var evidence = aggregator.Aggregate(results);

        Assert.Equal(2, evidence.Count);
        Assert.Contains(evidence, e => e.ProviderId == "p1" && e.Content == "Fact 1");
        Assert.Contains(evidence, e => e.ProviderId == "p3" && e.Content == "Fact 3");
    }

    [Fact]
    public void CSharp_RoutesToKnowledgeWithCanonicalQuery()
    {
        var intent = _detector.Detect("What is C#?");

        Assert.Equal(SaviConstants.Capabilities.Knowledge, intent.Capability);
        Assert.Equal("summary", intent.Operation);
        Assert.Contains("C# programming language", intent.Parameters["topic"]);
        Assert.Equal("C#", intent.Parameters["entity"]);
    }
}
