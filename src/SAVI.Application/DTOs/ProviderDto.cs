namespace SAVI.Application.DTOs;

public sealed record ProviderStatusDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Capability { get; init; } = string.Empty;
    public string Endpoint { get; init; } = string.Empty;
    public bool RequiresAuth { get; init; }
    public bool IsPaid { get; init; }
    public bool IsHealthy { get; init; }
    public int Priority { get; init; }
    public double ReliabilityScore { get; init; }
    public double LatencyMs { get; init; }
    public DateTimeOffset? LastSuccessfulCall { get; init; }
    public bool IsEnabled { get; init; } = true;
}

public sealed record CreateGenericProviderDto
{
    public string Name { get; init; } = string.Empty;
    public string Capability { get; init; } = string.Empty;
    public string Method { get; init; } = "GET";
    public string Endpoint { get; init; } = string.Empty;
    public Dictionary<string, string>? Headers { get; init; }
    public Dictionary<string, string>? Parameters { get; init; }
    public Dictionary<string, string>? ResponseMapping { get; init; }
    public int Priority { get; init; } = 100;
    public int RateLimitPerMin { get; init; } = 60;
    public int TimeoutSeconds { get; init; } = 15;
}

public sealed record TestProviderDto
{
    public string Endpoint { get; init; } = string.Empty;
    public string Method { get; init; } = "GET";
    public Dictionary<string, string>? Parameters { get; init; }
}
