namespace SAVI.Core.Models;

public sealed record SkillContext
{
    public string Prompt { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
    public string? UserId { get; init; }
    public string? UserName { get; init; }
    public string? ConversationId { get; init; }
    public string? TaskId { get; init; }
    public bool UserConfirmed { get; init; }
}

public sealed record SkillResult
{
    public bool Success { get; init; }
    public object? Data { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Error { get; init; }
    public bool Retryable { get; init; }
    public double Confidence { get; init; } = 1.0;
    public long ExecutionTimeMs { get; init; }

    public static SkillResult Succeeded(string message, object? data = null, double confidence = 1.0, long timeMs = 0) =>
        new() { Success = true, Message = message, Data = data, Confidence = confidence, ExecutionTimeMs = timeMs };

    public static SkillResult Failed(string error, bool retryable = false, long timeMs = 0) =>
        new() { Success = false, Error = error, Message = error, Retryable = retryable, Confidence = 0.0, ExecutionTimeMs = timeMs };
}
