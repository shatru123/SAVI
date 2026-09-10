using SAVI.Core.Entities;

namespace SAVI.Application.DTOs;

public sealed class AdminUserSummaryDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public bool IsActive { get; set; }
    public int ConversationCount { get; set; }
    public int MemoryCount { get; set; }
    public int TaskCount { get; set; }
}

public sealed class AdminUserDetailsDto
{
    public UserDto User { get; set; } = new();
    public List<ConversationSummaryDto> Conversations { get; set; } = new();
    public List<MemoryItemDto> MemoryItems { get; set; } = new();
    public List<TaskItemDto> Tasks { get; set; } = new();
    public List<AuditLogEntry> RecentAuditLogs { get; set; } = new();
}

public sealed class PlatformMetricsDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalConversations { get; set; }
    public int TotalMessages { get; set; }
    public int TotalMemoryItems { get; set; }
    public int TotalTasks { get; set; }
    public int ProviderCount { get; set; }
    public string SystemStatus { get; set; } = "Healthy";
}

public sealed class AdminConversationDetailDto
{
    public string ConversationId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserDisplayName { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<MessageDto> Messages { get; set; } = new();
}
