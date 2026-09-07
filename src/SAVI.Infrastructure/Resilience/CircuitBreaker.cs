using System.Collections.Concurrent;
using SAVI.Core.Enums;

namespace SAVI.Infrastructure.Resilience;

public class ProviderTelemetryRecord
{
    public string ProviderId { get; set; } = string.Empty;
    public CircuitBreakerState State { get; set; } = CircuitBreakerState.Healthy;
    public int ConsecutiveFailures { get; set; }
    public DateTimeOffset? LastFailureTime { get; set; }
    public DateTimeOffset? LastSuccessTime { get; set; }
    public DateTimeOffset? RateLimitResetUntil { get; set; }
    public TimeSpan OpenCooldown { get; set; } = TimeSpan.FromSeconds(30);

    // Rolling latency & success window
    private readonly ConcurrentQueue<(DateTimeOffset Time, long LatencyMs, bool Success)> _recentCalls = new();
    private const int MaxRecentCalls = 30;

    public void RecordSuccess(long latencyMs)
    {
        ConsecutiveFailures = 0;
        LastSuccessTime = DateTimeOffset.UtcNow;
        State = CircuitBreakerState.Healthy;
        OpenCooldown = TimeSpan.FromSeconds(30);

        EnqueueCall(latencyMs, true);
    }

    public void RecordFailure(long latencyMs, bool isRateLimited = false, TimeSpan? retryAfter = null)
    {
        ConsecutiveFailures++;
        LastFailureTime = DateTimeOffset.UtcNow;

        if (isRateLimited)
        {
            RateLimitResetUntil = DateTimeOffset.UtcNow.Add(retryAfter ?? TimeSpan.FromSeconds(60));
            State = CircuitBreakerState.Degraded;
        }
        else if (ConsecutiveFailures >= 3)
        {
            State = CircuitBreakerState.Open;
        }
        else
        {
            State = CircuitBreakerState.Degraded;
        }

        EnqueueCall(latencyMs, false);
    }

    private void EnqueueCall(long latencyMs, bool success)
    {
        _recentCalls.Enqueue((DateTimeOffset.UtcNow, latencyMs, success));
        while (_recentCalls.Count > MaxRecentCalls)
        {
            _recentCalls.TryDequeue(out _);
        }
    }

    public CircuitBreakerState GetCurrentState()
    {
        if (State == CircuitBreakerState.Open && LastFailureTime.HasValue)
        {
            if (DateTimeOffset.UtcNow - LastFailureTime.Value > OpenCooldown)
            {
                State = CircuitBreakerState.HalfOpen;
            }
        }

        if (RateLimitResetUntil.HasValue && DateTimeOffset.UtcNow >= RateLimitResetUntil.Value)
        {
            RateLimitResetUntil = null;
            if (State == CircuitBreakerState.Degraded)
            {
                State = CircuitBreakerState.Healthy;
            }
        }

        return State;
    }

    public bool IsRateLimited()
    {
        return RateLimitResetUntil.HasValue && DateTimeOffset.UtcNow < RateLimitResetUntil.Value;
    }

    public double GetSuccessRate()
    {
        var snapshot = _recentCalls.ToArray();
        if (snapshot.Length == 0) return 1.0;
        return (double)snapshot.Count(c => c.Success) / snapshot.Length;
    }

    public TimeSpan GetP95Latency()
    {
        var snapshot = _recentCalls.ToArray();
        if (snapshot.Length == 0) return TimeSpan.FromMilliseconds(200);
        var sorted = snapshot.Select(c => c.LatencyMs).OrderBy(l => l).ToList();
        var idx = (int)Math.Ceiling(sorted.Count * 0.95) - 1;
        return TimeSpan.FromMilliseconds(sorted[Math.Clamp(idx, 0, sorted.Count - 1)]);
    }

    public TimeSpan GetAverageLatency()
    {
        var snapshot = _recentCalls.ToArray();
        if (snapshot.Length == 0) return TimeSpan.FromMilliseconds(200);
        return TimeSpan.FromMilliseconds(snapshot.Average(c => c.LatencyMs));
    }
}

public class CircuitBreakerRegistry
{
    private readonly ConcurrentDictionary<string, ProviderTelemetryRecord> _records = new();

    public ProviderTelemetryRecord GetRecord(string providerId)
    {
        return _records.GetOrAdd(providerId, id => new ProviderTelemetryRecord { ProviderId = id });
    }

    public IReadOnlyCollection<ProviderTelemetryRecord> GetAllRecords()
    {
        return _records.Values.ToList();
    }
}
