namespace SAVI.Core.Entities;

public sealed class Conversation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = "New Conversation";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsArchived { get; set; }
    public string? Summary { get; set; }
    public string? UserId { get; set; }
    public User? User { get; set; }
    public List<Message> Messages { get; set; } = new();
}
