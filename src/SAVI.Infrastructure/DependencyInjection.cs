using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SAVI.Application.Interfaces;
using SAVI.Application.Services;
using SAVI.Core.Interfaces;
using SAVI.Infrastructure.Audit;
using SAVI.Infrastructure.Caching;
using SAVI.Infrastructure.Persistence;
using SAVI.Infrastructure.Providers;
using SAVI.Infrastructure.Providers.Ai;
using SAVI.Infrastructure.Providers.Books;
using SAVI.Infrastructure.Providers.Calculator;
using SAVI.Infrastructure.Providers.Currency;
using SAVI.Infrastructure.Providers.Finance;
using SAVI.Infrastructure.Providers.GitHub;
using SAVI.Infrastructure.Providers.Knowledge;
using SAVI.Infrastructure.Providers.Location;
using SAVI.Infrastructure.Providers.News;
using SAVI.Infrastructure.Providers.Research;
using SAVI.Infrastructure.Providers.Search;
using SAVI.Infrastructure.Providers.System;
using SAVI.Infrastructure.Providers.Weather;
using SAVI.Infrastructure.Repositories;
using SAVI.Infrastructure.Resilience;
using SAVI.Infrastructure.Speech;

namespace SAVI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSaviInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var dbPath = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=savi.db";

        services.AddDbContext<SaviDbContext>(options =>
        {
            options.UseSqlite(dbPath);
        });

        // Repositories
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IMemoryRepository, MemoryRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IProviderDefinitionRepository, ProviderDefinitionRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();

        // Application Services
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IMemoryService, MemoryService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<ITaskStateMachine>(sp => (TaskService)sp.GetRequiredService<ITaskService>());
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddSingleton<ISpeechService, SystemSpeechService>();

        // Resilience & Caching
        services.AddMemoryCache();
        services.AddSingleton<CircuitBreakerRegistry>();
        services.AddSingleton<ProviderScorer>();
        services.AddSingleton<IProviderCache, ProviderCache>();

        // Providers & HTTP Clients
        services.AddHttpClient();

        // Built-in Deterministic & Public Providers
        services.AddScoped<ICapabilityProvider, SystemInfoProvider>();
        services.AddScoped<ICapabilityProvider, CalculatorProvider>();
        services.AddScoped<ICapabilityProvider, OpenMeteoWeatherProvider>();
        services.AddScoped<ICapabilityProvider, WttrInWeatherProvider>();
        services.AddScoped<ICapabilityProvider, FrankfurterCurrencyProvider>();
        services.AddScoped<ICapabilityProvider, WikipediaKnowledgeProvider>();
        services.AddScoped<ICapabilityProvider, WikidataKnowledgeProvider>();
        services.AddScoped<ICapabilityProvider, CrossrefResearchProvider>();
        services.AddScoped<ICapabilityProvider, OpenLibraryProvider>();
        services.AddScoped<ICapabilityProvider, HackerNewsProvider>();
        services.AddScoped<ICapabilityProvider, NominatimLocationProvider>();
        services.AddScoped<ICapabilityProvider, CoinGeckoCryptoProvider>();
        services.AddScoped<ICapabilityProvider, GitHubPublicProvider>();
        services.AddScoped<ICapabilityProvider, DuckDuckGoSearchProvider>();
        services.AddScoped<ISearchProvider, DuckDuckGoSearchProvider>();

        // Optional Reasoning Providers
        services.AddScoped<ICapabilityProvider, FreeAiProvider>();
        services.AddScoped<ICapabilityProvider, OptionalOllamaProvider>();

        services.AddScoped<IProviderRegistry, ProviderRegistry>();

        return services;
    }

    public static async Task InitializeSaviDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SaviDbContext>();
        await db.Database.EnsureCreatedAsync();

        // Seed default settings if empty
        var settingsRepo = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
        await settingsRepo.GetSettingsAsync();
    }
}
