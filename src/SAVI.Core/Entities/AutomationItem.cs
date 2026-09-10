namespace SAVI.Core.Entities;

public sealed class AutomationItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TriggerType { get; set; } = "Interval";
    public int? IntervalSeconds { get; set; } = 3600;
    public string? CronExpression { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset? LastExecutedAt { get; set; }
    public DateTimeOffset? NextExecutionAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
