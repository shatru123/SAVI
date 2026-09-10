namespace SAVI.Core.Entities;

public sealed class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "User"; // "Owner" or "User"
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    public List<Conversation> Conversations { get; set; } = new();
    public List<MemoryItem> MemoryItems { get; set; } = new();
    public List<TaskItem> TaskItems { get; set; } = new();
}
