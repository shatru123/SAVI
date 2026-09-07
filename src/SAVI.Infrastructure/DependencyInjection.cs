using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SAVI.Application.Interfaces;
using SAVI.Application.Services;
using SAVI.Core.Interfaces;
using SAVI.Infrastructure.Audit;
using SAVI.Infrastructure.Persistence;
using SAVI.Infrastructure.Providers;
using SAVI.Infrastructure.Providers.Ai;
using SAVI.Infrastructure.Providers.Calculator;
using SAVI.Infrastructure.Providers.Currency;
using SAVI.Infrastructure.Providers.GitHub;
using SAVI.Infrastructure.Providers.Knowledge;
using SAVI.Infrastructure.Providers.Search;
using SAVI.Infrastructure.Providers.System;
using SAVI.Infrastructure.Providers.Weather;
using SAVI.Infrastructure.Repositories;
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

        // Providers & HTTP Clients
        services.AddHttpClient();

        services.AddSingleton<ICapabilityProvider, OpenMeteoWeatherProvider>();
        services.AddSingleton<ICapabilityProvider, WttrInWeatherProvider>();
        services.AddSingleton<ICapabilityProvider, FrankfurterCurrencyProvider>();
        services.AddSingleton<ICapabilityProvider, WikipediaKnowledgeProvider>();
        services.AddSingleton<ICapabilityProvider, DuckDuckGoSearchProvider>();
        services.AddSingleton<ISearchProvider, DuckDuckGoSearchProvider>();
        services.AddSingleton<ICapabilityProvider, GitHubPublicProvider>();
        services.AddSingleton<ICapabilityProvider, SystemInfoProvider>();
        services.AddSingleton<ICapabilityProvider, CalculatorProvider>();
        services.AddSingleton<ICapabilityProvider, FreeAiProvider>();
        services.AddSingleton<ICapabilityProvider, OptionalOllamaProvider>();

        services.AddSingleton<IProviderRegistry, ProviderRegistry>();

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
