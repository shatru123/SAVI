using System.Diagnostics;
using System.Runtime.InteropServices;
using SAVI.Core.Constants;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Core.ValueObjects;

namespace SAVI.Infrastructure.Providers.System;

public class SystemInfoProvider : ICapabilityProvider
{
    private static readonly DateTime StartTime = DateTime.UtcNow;

    public string Id => SaviConstants.Providers.LocalSystem;
    public string Name => "Local Host System Diagnostics (Local/Zero-Cost)";
    public IReadOnlyCollection<string> Capabilities => new[] { SaviConstants.Capabilities.System, SaviConstants.Capabilities.Time };
    public int Priority => 5;

    public bool CanHandle(TaskRequest request)
    {
        return request.Capability.Equals(SaviConstants.Capabilities.System, StringComparison.OrdinalIgnoreCase) ||
               request.Capability.Equals(SaviConstants.Capabilities.Time, StringComparison.OrdinalIgnoreCase);
    }

    public Task<ProviderResult> ExecuteAsync(TaskRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Capability.Equals(SaviConstants.Capabilities.Time, StringComparison.OrdinalIgnoreCase))
        {
            var now = DateTimeOffset.Now;
            var timeData = new
            {
                CurrentLocalTime = now.ToString("F"),
                CurrentUtcTime = DateTimeOffset.UtcNow.ToString("F"),
                TimeZone = TimeZoneInfo.Local.DisplayName,
                IsDaylightSaving = TimeZoneInfo.Local.IsDaylightSavingTime(now)
            };

            var timeSource = new SourceReference
            {
                Title = "Local System Clock",
                Url = "local://clock",
                SourceName = "Local OS Time",
                Snippet = $"Local Time: {now:F} ({TimeZoneInfo.Local.StandardName})",
                ReliabilityScore = 1.0
            };

            return Task.FromResult(ProviderResult.Succeeded(Id, Name, timeData, confidence: 1.0, sources: new[] { timeSource }));
        }

        var proc = Process.GetCurrentProcess();
        var osDesc = RuntimeInformation.OSDescription;
        var arch = RuntimeInformation.OSArchitecture.ToString();
        var framework = RuntimeInformation.FrameworkDescription;
        var uptime = DateTime.UtcNow - StartTime;
        var memoryUsedMb = proc.WorkingSet64 / (1024 * 1024);
        var cores = Environment.ProcessorCount;

        var systemData = new
        {
            OperatingSystem = osDesc,
            Architecture = arch,
            Framework = framework,
            ProcessorCores = cores,
            ProcessMemoryMb = memoryUsedMb,
            Uptime = $"{uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s",
            MachineName = Environment.MachineName,
            UserName = Environment.UserName
        };

        var source = new SourceReference
        {
            Title = "Host System Telemetry",
            Url = "local://system",
            SourceName = "Host System",
            Snippet = $"{osDesc} ({arch}), .NET {framework}, Process Memory: {memoryUsedMb} MB, Uptime: {uptime.Hours}h {uptime.Minutes}m.",
            ReliabilityScore = 1.0
        };

        return Task.FromResult(ProviderResult.Succeeded(Id, Name, systemData, confidence: 1.0, sources: new[] { source }));
    }

    public Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }
}
