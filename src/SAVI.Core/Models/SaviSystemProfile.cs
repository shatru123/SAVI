namespace SAVI.Core.Models;

public record SaviSystemProfile
{
    public string Name { get; init; } = "SAVI";
    public string FullName { get; init; } = "Shatru's Adaptive Virtual Intelligence";
    public string Version { get; init; } = "1.0.0";
    public string Framework { get; init; } = ".NET 10";
    public string Language { get; init; } = "C# 13";
    public string Frontend { get; init; } = "ASP.NET Core Blazor (InteractiveServer) with Cyberpunk HUD";
    public string Backend { get; init; } = "ASP.NET Core on .NET 10";
    public string Database { get; init; } = "SQLite (savi.db) via Entity Framework Core";
    public string Creator { get; init; } = "Shatrughna Ambhore";
    public string Purpose { get; init; } = "Personal AI assistant, conversational voice intelligence, and sovereign task execution platform";
    public string CostModel { get; init; } = "100% Free, sovereign, zero mandatory API keys";
    public string DeploymentEnvironment { get; init; } = "Cross-platform (macOS, Linux, Windows), responsive web";
    public IReadOnlyList<string> InteractionModes { get; init; } = new[] { "Chat Deck", "Real-Time Conversational Voice" };
    public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> BuiltInProviders { get; init; } = Array.Empty<string>();
    public int ActiveProvidersCount { get; init; }
    public bool VoiceSupported { get; init; } = true;
    public bool ChatSupported { get; init; } = true;
    public bool LongTermMemorySupported { get; init; } = true;
    public bool MultiStepTasksSupported { get; init; } = true;
}
