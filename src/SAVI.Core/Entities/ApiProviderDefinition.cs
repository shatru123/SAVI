namespace SAVI.Core.Entities;

public sealed class ApiProviderDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Capability { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public string Endpoint { get; set; } = string.Empty;
    public string? HeadersJson { get; set; }
    public string? ParametersJson { get; set; }
    public string? ResponseMappingJson { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; } = 100;
    public int RateLimitPerMin { get; set; } = 60;
    public int TimeoutSeconds { get; set; } = 15;
    public string HealthStatus { get; set; } = "Healthy";
    public DateTimeOffset? LastCheckedAt { get; set; }
    public string? LastError { get; set; }
}
