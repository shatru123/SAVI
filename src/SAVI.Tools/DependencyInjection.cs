using Microsoft.Extensions.DependencyInjection;
using SAVI.Core.Interfaces;
using SAVI.Tools.Browser;
using SAVI.Tools.Documents;
using SAVI.Tools.FileSystem;
using SAVI.Tools.Notifications;
using SAVI.Tools.Security;
using SAVI.Tools.Skills;
using SAVI.Tools.Terminal;

namespace SAVI.Tools;

public static class DependencyInjection
{
    public static IServiceCollection AddSaviTools(this IServiceCollection services)
    {
        services.AddScoped<IPermissionGuard, PermissionGuard>();

        services.AddScoped<ITool, FileSystemTool>();
        services.AddScoped<ITool, TerminalTool>();
        services.AddScoped<ITool, BrowserAgentTool>();
        services.AddScoped<ITool, DocumentReaderTool>();
        services.AddScoped<ITool, NotificationTool>();

        services.AddScoped<IToolRegistry, ToolRegistry>();

        // Skills Architecture
        services.AddScoped<ISaviSkill, CalculatorSkill>();
        services.AddScoped<ISaviSkill, WeatherSkill>();
        services.AddScoped<ISaviSkill, CurrencySkill>();
        services.AddScoped<ISaviSkill, SearchSkill>();
        services.AddScoped<ISaviSkill, WikipediaSkill>();
        services.AddScoped<ISaviSkill, GitHubSkill>();
        services.AddScoped<ISaviSkill, SystemSkill>();
        services.AddScoped<ISaviSkill, MemorySkill>();
        services.AddScoped<ISaviSkill, TasksSkill>();
        services.AddScoped<ISaviSkill, TravelSkill>();

        services.AddScoped<ISkillRegistry, Skills.SkillRegistry>();

        return services;
    }
}
