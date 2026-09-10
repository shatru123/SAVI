using SAVI.Application.DTOs;
using SAVI.Core.Entities;

namespace SAVI.Application.Interfaces;

public interface IAdminService
{
    Task<PlatformMetricsDto> GetPlatformMetricsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminUserSummaryDto>> GetUsersAsync(string? query, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> GetUserCountAsync(string? query, CancellationToken cancellationToken = default);
    Task<AdminUserDetailsDto?> GetUserDetailsAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConversationSummaryDto>> GetUserConversationsAsync(string userId, CancellationToken cancellationToken = default);
    Task<AdminConversationDetailDto?> GetConversationDetailsForAdminAsync(string conversationId, CancellationToken cancellationToken = default);
    Task<bool> SetUserStatusAsync(string userId, bool isActive, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditLogEntry>> GetAuditLogsAsync(int take = 100, CancellationToken cancellationToken = default);
}
