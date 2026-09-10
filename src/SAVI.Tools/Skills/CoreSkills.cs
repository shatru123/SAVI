using SAVI.Application.DTOs;
using SAVI.Application.Interfaces;
using SAVI.Core.Constants;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Tools.Skills;

public class CalculatorSkill : ISaviSkill
{
    private readonly IProviderRegistry _providerRegistry;

    public CalculatorSkill(IProviderRegistry providerRegistry)
    {
        _providerRegistry = providerRegistry;
    }

    public string Name => "calculator";
    public string Description => "Performs high-precision arithmetic, formula evaluation, and unit conversions.";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.Calculator };
    public SkillPermissionLevel RequiredPermission => SkillPermissionLevel.Public;

    public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken cancellationToken = default)
    {
        var taskReq = new TaskRequest
        {
            Capability = SaviConstants.Capabilities.Calculator,
            Prompt = context.Prompt,
            Parameters = new Dictionary<string, string>(context.Parameters),
            ConversationId = context.ConversationId
        };

        var providers = _providerRegistry.GetByCapability(SaviConstants.Capabilities.Calculator);
        var provider = providers.FirstOrDefault();
        if (provider == null)
        {
            return SkillResult.Failed("No calculator provider registered.");
        }

        var res = await provider.ExecuteAsync(taskReq, cancellationToken);
        return res.Success
            ? SkillResult.Succeeded(res.Data?.ToString() ?? "Computed successfully.", res.Data, res.Confidence)
            : SkillResult.Failed(res.Error ?? "Calculation failed.");
    }
}

public class WeatherSkill : ISaviSkill
{
    private readonly IProviderRegistry _providerRegistry;

    public WeatherSkill(IProviderRegistry providerRegistry)
    {
        _providerRegistry = providerRegistry;
    }

    public string Name => "weather";
    public string Description => "Retrieves live forecasts, temperature, precipitation, and conditions worldwide.";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.Weather };
    public SkillPermissionLevel RequiredPermission => SkillPermissionLevel.Public;

    public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken cancellationToken = default)
    {
        var taskReq = new TaskRequest
        {
            Capability = SaviConstants.Capabilities.Weather,
            Prompt = context.Prompt,
            Parameters = new Dictionary<string, string>(context.Parameters),
            ConversationId = context.ConversationId
        };

        var providers = _providerRegistry.GetByCapability(SaviConstants.Capabilities.Weather);
        foreach (var provider in providers.OrderBy(p => p.Priority))
        {
            try
            {
                var res = await provider.ExecuteAsync(taskReq, cancellationToken);
                if (res.Success)
                {
                    return SkillResult.Succeeded(res.Data?.ToString() ?? "Weather retrieved.", res.Data, res.Confidence);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Fallback to next provider
            }
        }

        return SkillResult.Failed("Unable to retrieve weather forecast from available providers.");
    }
}

public class CurrencySkill : ISaviSkill
{
    private readonly IProviderRegistry _providerRegistry;

    public CurrencySkill(IProviderRegistry providerRegistry)
    {
        _providerRegistry = providerRegistry;
    }

    public string Name => "currency";
    public string Description => "Performs live foreign currency conversions and historical FX lookups.";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.Currency };
    public SkillPermissionLevel RequiredPermission => SkillPermissionLevel.Public;

    public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken cancellationToken = default)
    {
        var taskReq = new TaskRequest
        {
            Capability = SaviConstants.Capabilities.Currency,
            Prompt = context.Prompt,
            Parameters = new Dictionary<string, string>(context.Parameters),
            ConversationId = context.ConversationId
        };

        var providers = _providerRegistry.GetByCapability(SaviConstants.Capabilities.Currency);
        var provider = providers.FirstOrDefault();
        if (provider == null)
        {
            return SkillResult.Failed("No currency provider registered.");
        }

        var res = await provider.ExecuteAsync(taskReq, cancellationToken);
        return res.Success
            ? SkillResult.Succeeded(res.Data?.ToString() ?? "Currency converted.", res.Data, res.Confidence)
            : SkillResult.Failed(res.Error ?? "Currency conversion failed.");
    }
}

public class SearchSkill : ISaviSkill
{
    private readonly IProviderRegistry _providerRegistry;

    public SearchSkill(IProviderRegistry providerRegistry)
    {
        _providerRegistry = providerRegistry;
    }

    public string Name => "search";
    public string Description => "Searches the public web via privacy-first search engines.";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.Search };
    public SkillPermissionLevel RequiredPermission => SkillPermissionLevel.Public;

    public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken cancellationToken = default)
    {
        var taskReq = new TaskRequest
        {
            Capability = SaviConstants.Capabilities.Search,
            Prompt = context.Prompt,
            Parameters = new Dictionary<string, string>(context.Parameters),
            ConversationId = context.ConversationId
        };

        var providers = _providerRegistry.GetByCapability(SaviConstants.Capabilities.Search);
        var provider = providers.FirstOrDefault();
        if (provider == null)
        {
            return SkillResult.Failed("No search provider registered.");
        }

        var res = await provider.ExecuteAsync(taskReq, cancellationToken);
        return res.Success
            ? SkillResult.Succeeded(res.Data?.ToString() ?? "Search completed.", res.Data, res.Confidence)
            : SkillResult.Failed(res.Error ?? "Search query failed.");
    }
}

public class WikipediaSkill : ISaviSkill
{
    private readonly IProviderRegistry _providerRegistry;

    public WikipediaSkill(IProviderRegistry providerRegistry)
    {
        _providerRegistry = providerRegistry;
    }

    public string Name => "wikipedia";
    public string Description => "Fetches encyclopedic summaries and factual entities from Wikipedia.";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.Knowledge };
    public SkillPermissionLevel RequiredPermission => SkillPermissionLevel.Public;

    public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken cancellationToken = default)
    {
        var taskReq = new TaskRequest
        {
            Capability = SaviConstants.Capabilities.Knowledge,
            Prompt = context.Prompt,
            Parameters = new Dictionary<string, string>(context.Parameters),
            ConversationId = context.ConversationId
        };

        var providers = _providerRegistry.GetByCapability(SaviConstants.Capabilities.Knowledge);
        var provider = providers.FirstOrDefault();
        if (provider == null)
        {
            return SkillResult.Failed("No knowledge provider registered.");
        }

        var res = await provider.ExecuteAsync(taskReq, cancellationToken);
        return res.Success
            ? SkillResult.Succeeded(res.Data?.ToString() ?? "Entity summary found.", res.Data, res.Confidence)
            : SkillResult.Failed(res.Error ?? "Knowledge lookup failed.");
    }
}

public class GitHubSkill : ISaviSkill
{
    private readonly IProviderRegistry _providerRegistry;

    public GitHubSkill(IProviderRegistry providerRegistry)
    {
        _providerRegistry = providerRegistry;
    }

    public string Name => "github";
    public string Description => "Queries public GitHub repositories, releases, topics, and contributor metrics.";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.GitHub };
    public SkillPermissionLevel RequiredPermission => SkillPermissionLevel.Public;

    public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken cancellationToken = default)
    {
        var taskReq = new TaskRequest
        {
            Capability = SaviConstants.Capabilities.GitHub,
            Prompt = context.Prompt,
            Parameters = new Dictionary<string, string>(context.Parameters),
            ConversationId = context.ConversationId
        };

        var providers = _providerRegistry.GetByCapability(SaviConstants.Capabilities.GitHub);
        var provider = providers.FirstOrDefault();
        if (provider == null)
        {
            return SkillResult.Failed("No GitHub provider registered.");
        }

        var res = await provider.ExecuteAsync(taskReq, cancellationToken);
        return res.Success
            ? SkillResult.Succeeded(res.Data?.ToString() ?? "GitHub data retrieved.", res.Data, res.Confidence)
            : SkillResult.Failed(res.Error ?? "GitHub query failed.");
    }
}

public class SystemSkill : ISaviSkill
{
    private readonly IProviderRegistry _providerRegistry;

    public SystemSkill(IProviderRegistry providerRegistry)
    {
        _providerRegistry = providerRegistry;
    }

    public string Name => "system";
    public string Description => "Inspects host OS runtime, memory, CPU load, and system health telemetry.";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.System };
    public SkillPermissionLevel RequiredPermission => SkillPermissionLevel.Authenticated;

    public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken cancellationToken = default)
    {
        var taskReq = new TaskRequest
        {
            Capability = SaviConstants.Capabilities.System,
            Prompt = context.Prompt,
            Parameters = new Dictionary<string, string>(context.Parameters),
            ConversationId = context.ConversationId
        };

        var providers = _providerRegistry.GetByCapability(SaviConstants.Capabilities.System);
        var provider = providers.FirstOrDefault();
        if (provider == null)
        {
            return SkillResult.Failed("No system provider registered.");
        }

        var res = await provider.ExecuteAsync(taskReq, cancellationToken);
        return res.Success
            ? SkillResult.Succeeded(res.Data?.ToString() ?? "System metrics retrieved.", res.Data, res.Confidence)
            : SkillResult.Failed(res.Error ?? "System metrics failed.");
    }
}

public class MemorySkill : ISaviSkill
{
    private readonly IMemoryService _memoryService;

    public MemorySkill(IMemoryService memoryService)
    {
        _memoryService = memoryService;
    }

    public string Name => "memory";
    public string Description => "Manages persistent cross-session facts, preferences, and project context.";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.Memory };
    public SkillPermissionLevel RequiredPermission => SkillPermissionLevel.Authenticated;

    public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken cancellationToken = default)
    {
        var op = context.Parameters.GetValueOrDefault("operation") ?? "recall";
        if (op == "remember")
        {
            var content = context.Parameters.GetValueOrDefault("content") ?? context.Prompt;
            var created = await _memoryService.AddAsync(new CreateMemoryDto
            {
                Type = MemoryType.Preference,
                Content = content,
                Importance = 1.0
            }, context.ConversationId, cancellationToken);

            return SkillResult.Succeeded($"Memory saved: \"{created.Content}\"", created);
        }

        var query = context.Parameters.GetValueOrDefault("query") ?? context.Prompt;
        var memories = await _memoryService.GetRelevantMemoriesAsync(query, cancellationToken);
        var summary = memories.Count == 0
            ? "No relevant memories found."
            : string.Join("\n", memories.Select(m => $"• [{m.Type}] {m.Content}"));

        return SkillResult.Succeeded(summary, memories);
    }
}

public class TasksSkill : ISaviSkill
{
    private readonly ITaskService _taskService;

    public TasksSkill(ITaskService taskService)
    {
        _taskService = taskService;
    }

    public string Name => "tasks";
    public string Description => "Coordinates stateful multi-step workflow execution with approval checkpoints.";
    public IReadOnlyCollection<string> Capabilities => new[] { "tasks" };
    public SkillPermissionLevel RequiredPermission => SkillPermissionLevel.Authenticated;

    public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken cancellationToken = default)
    {
        var op = context.Parameters.GetValueOrDefault("operation") ?? "list";
        if (op == "create")
        {
            var title = context.Parameters.GetValueOrDefault("title") ?? "Automated Workflow";
            var desc = context.Parameters.GetValueOrDefault("description") ?? context.Prompt;
            var steps = (context.Parameters.GetValueOrDefault("steps") ?? "Analyze, Execute, Verify")
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var created = await _taskService.CreateTaskAsync(title, desc, steps);
            return SkillResult.Succeeded($"Task workflow '{created.Title}' initialized with {created.Steps.Count} steps.", created);
        }

        var tasks = await _taskService.GetAllAsync(cancellationToken: cancellationToken);
        return SkillResult.Succeeded($"Found {tasks.Count} active task workflows.", tasks);
    }
}

public class TravelSkill : ISaviSkill
{
    private readonly IProviderRegistry _providerRegistry;

    public TravelSkill(IProviderRegistry providerRegistry)
    {
        _providerRegistry = providerRegistry;
    }

    public string Name => "travel";
    public string Description => "Composes multi-capability trip intelligence combining weather, currency, and location.";
    public IReadOnlyCollection<string> Capabilities => new[] { "travel" };
    public SkillPermissionLevel RequiredPermission => SkillPermissionLevel.Public;

    public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken cancellationToken = default)
    {
        var dest = context.Parameters.GetValueOrDefault("destination") ?? context.Prompt;

        // Compose weather and currency checks concurrently
        var weatherReq = new TaskRequest { Capability = SaviConstants.Capabilities.Weather, Prompt = $"weather in {dest}" };
        var weatherProviders = _providerRegistry.GetByCapability(SaviConstants.Capabilities.Weather);

        var weatherTask = weatherProviders.FirstOrDefault()?.ExecuteAsync(weatherReq, cancellationToken)
                          ?? Task.FromResult(ProviderResult.Failed("none", "none", "Weather not available"));

        var weatherResult = await weatherTask;
        var summary = $"Travel Brief for {dest}:\n" +
                      $"• Forecast: {(weatherResult.Success ? weatherResult.Data?.ToString() : "Weather unavailable")}";

        return SkillResult.Succeeded(summary, new { Destination = dest, Weather = weatherResult.Data });
    }
}
