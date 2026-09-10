namespace SAVI.Application.DTOs;

public sealed record AutomationDto(
    string Id,
    string? UserId,
    string Name,
    string Description,
    string TriggerType,
    int? IntervalSeconds,
    string? CronExpression,
    string Prompt,
    bool IsEnabled,
    DateTimeOffset? LastExecutedAt,
    DateTimeOffset? NextExecutionAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record CreateAutomationDto(
    string Name,
    string Description,
    string TriggerType,
    int? IntervalSeconds,
    string? CronExpression,
    string Prompt,
    bool IsEnabled = true
);

public sealed record UpdateAutomationDto(
    string Name,
    string Description,
    string TriggerType,
    int? IntervalSeconds,
    string? CronExpression,
    string Prompt,
    bool IsEnabled
);

public sealed record AutomationExecutionLogDto(
    string Id,
    string AutomationId,
    DateTimeOffset ExecutedAt,
    bool Success,
    string? Result,
    string? ErrorMessage,
    long DurationMs
);
