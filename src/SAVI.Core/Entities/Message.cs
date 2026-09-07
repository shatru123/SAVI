using SAVI.Core.Enums;

namespace SAVI.Core.Entities;

public sealed class Message
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ConversationId { get; set; } = string.Empty;
    public MessageRole Role { get; set; } = MessageRole.User;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public MessageType MessageType { get; set; } = MessageType.Text;
    public string? SourcesJson { get; set; }
    public string? ToolExecutionsJson { get; set; }
    public string? MetadataJson { get; set; }

    public Conversation? Conversation { get; set; }
}
