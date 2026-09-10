namespace SAVI.Core.Entities;

public sealed class AutomationExecutionLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AutomationId { get; set; } = string.Empty;
    public DateTimeOffset ExecutedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool Success { get; set; }
    public string? Result { get; set; }
    public string? ErrorMessage { get; set; }
    public long DurationMs { get; set; }
}
