using System.Text.Json;
using SAVI.Application.DTOs;
using SAVI.Application.Interfaces;
using SAVI.Core.Constants;
using SAVI.Core.Entities;
using SAVI.Core.Enums;
using SAVI.Core.ValueObjects;

namespace SAVI.Application.Services;

public class AdminService : IAdminService
{
    private readonly IUserRepository _userRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IMemoryRepository _memoryRepository;
    private readonly ITaskRepository _taskRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IProviderDefinitionRepository? _providerRepository;

    public AdminService(
        IUserRepository userRepository,
        IConversationRepository conversationRepository,
        IMemoryRepository memoryRepository,
        ITaskRepository taskRepository,
        IAuditRepository auditRepository,
        ICurrentUserService currentUserService,
        IProviderDefinitionRepository? providerRepository = null)
    {
        _userRepository = userRepository;
        _conversationRepository = conversationRepository;
        _memoryRepository = memoryRepository;
        _taskRepository = taskRepository;
        _auditRepository = auditRepository;
        _currentUserService = currentUserService;
        _providerRepository = providerRepository;
    }

    private void EnsureOwner()
    {
        if (!_currentUserService.IsOwner)
        {
            throw new UnauthorizedAccessException("Owner authorization required for administrative operations.");
        }
    }

    public async Task<PlatformMetricsDto> GetPlatformMetricsAsync(CancellationToken cancellationToken = default)
    {
        EnsureOwner();

        var totalUsers = await _userRepository.GetCountAsync(null, cancellationToken);
        var allUsers = await _userRepository.GetAllAsync(null, 0, 10000, cancellationToken);
        var activeUsers = allUsers.Count(u => u.IsActive);

        var totalConversations = await _conversationRepository.GetCountAsync(null, cancellationToken);
        var totalMessages = await _conversationRepository.GetTotalMessagesCountAsync(cancellationToken);
        var totalMemory = await _memoryRepository.GetCountAsync(userId: null, cancellationToken: cancellationToken);
        var allTasks = await _taskRepository.GetAllAsync(state: null, userId: null, cancellationToken: cancellationToken);
        var providerCount = _providerRepository != null ? (await _providerRepository.GetAllAsync(cancellationToken)).Count : 4;

        return new PlatformMetricsDto
        {
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            TotalConversations = totalConversations,
            TotalMessages = totalMessages,
            TotalMemoryItems = totalMemory,
            TotalTasks = allTasks.Count,
            ProviderCount = providerCount,
            SystemStatus = "Operational"
        };
    }

    public async Task<IReadOnlyList<AdminUserSummaryDto>> GetUsersAsync(string? query, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        EnsureOwner();

        var skip = Math.Max(0, (page - 1) * pageSize);
        var users = await _userRepository.GetAllAsync(query, skip, pageSize, cancellationToken);

        return users.Select(u => new AdminUserSummaryDto
        {
            Id = u.Id,
            Email = u.Email,
            DisplayName = u.DisplayName,
            Role = u.Role,
            CreatedAt = u.CreatedAt,
            LastLoginAt = u.LastLoginAt,
            IsActive = u.IsActive,
            ConversationCount = u.Conversations?.Count ?? 0,
            MemoryCount = u.MemoryItems?.Count ?? 0,
            TaskCount = u.TaskItems?.Count ?? 0
        }).ToList();
    }

    public async Task<int> GetUserCountAsync(string? query, CancellationToken cancellationToken = default)
    {
        EnsureOwner();
        return await _userRepository.GetCountAsync(query, cancellationToken);
    }

    public async Task<AdminUserDetailsDto?> GetUserDetailsAsync(string userId, CancellationToken cancellationToken = default)
    {
        EnsureOwner();

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return null;
        }

        var allAuditLogs = await _auditRepository.GetRecentAsync(100, cancellationToken);
        var userLogs = allAuditLogs
            .Where(a => a.UserId == userId || a.TargetUserId == userId)
            .ToList();

        return new AdminUserDetailsDto
        {
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                Role = user.Role,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                IsActive = user.IsActive
            },
            Conversations = user.Conversations?.Select(c => new ConversationSummaryDto
            {
                Id = c.Id,
                Title = c.Title,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt,
                IsArchived = c.IsArchived,
                MessageCount = c.Messages?.Count ?? 0,
                Summary = c.Summary
            }).OrderByDescending(c => c.UpdatedAt).ToList() ?? new(),
            MemoryItems = user.MemoryItems?.Select(m => new MemoryItemDto
            {
                Id = m.Id,
                Type = m.Type,
                Content = m.Content,
                Importance = m.Importance,
                Confidence = m.Confidence,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt,
                ExpiresAt = m.ExpiresAt,
                SourceConversationId = m.SourceConversationId
            }).OrderByDescending(m => m.CreatedAt).ToList() ?? new(),
            Tasks = user.TaskItems?.Select(t => new TaskItemDto
            {
                Id = t.Id,
                Title = t.Title,
                Description = t.Description,
                State = t.State,
                ProgressPercentage = t.ProgressPercentage,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                Steps = string.IsNullOrWhiteSpace(t.StepsJson) ? Array.Empty<TaskStepDto>() : (JsonSerializer.Deserialize<List<TaskStepDto>>(t.StepsJson) ?? new List<TaskStepDto>()),
                ResultSummary = t.ResultSummary,
                ErrorMessage = t.ErrorMessage
            }).OrderByDescending(t => t.CreatedAt).ToList() ?? new(),
            RecentAuditLogs = userLogs
        };
    }

    public async Task<IReadOnlyList<ConversationSummaryDto>> GetUserConversationsAsync(string userId, CancellationToken cancellationToken = default)
    {
        EnsureOwner();

        var convs = await _conversationRepository.GetAllAsync(userId: userId, includeArchived: true, cancellationToken);
        return convs.Select(c => new ConversationSummaryDto
        {
            Id = c.Id,
            Title = c.Title,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt,
            IsArchived = c.IsArchived,
            MessageCount = c.Messages?.Count ?? 0,
            Summary = c.Summary
        }).ToList();
    }

    public async Task<AdminConversationDetailDto?> GetConversationDetailsForAdminAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        EnsureOwner();

        var conv = await _conversationRepository.GetByIdAsync(conversationId, cancellationToken);
        if (conv == null)
        {
            return null;
        }

        string? userDisplayName = null;
        if (!string.IsNullOrEmpty(conv.UserId))
        {
            var user = await _userRepository.GetByIdAsync(conv.UserId, cancellationToken);
            userDisplayName = user?.DisplayName;
        }

        var messages = conv.Messages?.Select(m => new MessageDto
        {
            Id = m.Id,
            Role = m.Role,
            Content = m.Content,
            Timestamp = m.Timestamp,
            MessageType = m.MessageType,
            Sources = string.IsNullOrWhiteSpace(m.SourcesJson) ? null : JsonSerializer.Deserialize<List<SourceReference>>(m.SourcesJson),
            ToolExecutions = string.IsNullOrWhiteSpace(m.ToolExecutionsJson) ? null : JsonSerializer.Deserialize<List<ToolExecutionResult>>(m.ToolExecutionsJson),
            ActivityLogs = string.IsNullOrWhiteSpace(m.MetadataJson) ? null : JsonSerializer.Deserialize<List<string>>(m.MetadataJson)
        }).ToList() ?? new List<MessageDto>();

        return new AdminConversationDetailDto
        {
            ConversationId = conv.Id,
            UserId = conv.UserId,
            UserDisplayName = userDisplayName,
            Title = conv.Title,
            CreatedAt = conv.CreatedAt,
            UpdatedAt = conv.UpdatedAt,
            Messages = messages
        };
    }

    public async Task<bool> SetUserStatusAsync(string userId, bool isActive, CancellationToken cancellationToken = default)
    {
        EnsureOwner();

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            return false;
        }

        if (user.Role == SaviConstants.Roles.Owner && !isActive)
        {
            throw new InvalidOperationException("Cannot deactivate an Owner account.");
        }

        user.IsActive = isActive;
        await _userRepository.UpdateAsync(user, cancellationToken);

        await _auditRepository.AddAsync(new AuditLogEntry
        {
            Id = Guid.NewGuid().ToString(),
            UserId = _currentUserService.UserId,
            TargetUserId = userId,
            Action = isActive ? "UserActivated" : "UserDeactivated",
            ToolName = "AdminService",
            PermissionLevel = PermissionLevel.Dangerous,
            Status = "Success",
            Details = $"User {user.Email} status set to isActive={isActive} by {_currentUserService.DisplayName ?? "Owner"}",
            Timestamp = DateTimeOffset.UtcNow
        }, cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetAuditLogsAsync(int take = 100, CancellationToken cancellationToken = default)
    {
        EnsureOwner();
        return await _auditRepository.GetRecentAsync(take, cancellationToken);
    }
}
