namespace SAVI.Application.DTOs;

public sealed record DiagnosticsDto
{
    public string SystemName { get; init; } = "SAVI";
    public string Version { get; init; } = "1.0.0";
    public string OperatingSystem { get; init; } = string.Empty;
    public string Architecture { get; init; } = string.Empty;
    public string DotnetVersion { get; init; } = string.Empty;
    public TimeSpan Uptime { get; init; }
    public long MemoryUsedBytes { get; init; }
    public int ActiveConversationsCount { get; init; }
    public int MemoryItemsCount { get; init; }
    public int RegisteredProvidersCount { get; init; }
    public bool InternetAvailable { get; init; }
}
