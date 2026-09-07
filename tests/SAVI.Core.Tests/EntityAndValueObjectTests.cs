using SAVI.Core.Entities;
using SAVI.Core.Enums;
using SAVI.Core.ValueObjects;
using Xunit;

namespace SAVI.Core.Tests;

public class EntityAndValueObjectTests
{
    [Fact]
    public void Conversation_Initialization_ShouldHaveDefaultValues()
    {
        var conv = new Conversation();

        Assert.False(string.IsNullOrWhiteSpace(conv.Id));
        Assert.Equal("New Conversation", conv.Title);
        Assert.False(conv.IsArchived);
        Assert.NotNull(conv.Messages);
        Assert.Empty(conv.Messages);
    }

    [Fact]
    public void Message_Initialization_ShouldRetainProperties()
    {
        var msg = new Message
        {
            ConversationId = "c1",
            Role = MessageRole.User,
            Content = "Hello SAVI",
            MessageType = MessageType.Text
        };

        Assert.Equal("c1", msg.ConversationId);
        Assert.Equal(MessageRole.User, msg.Role);
        Assert.Equal("Hello SAVI", msg.Content);
        Assert.Equal(MessageType.Text, msg.MessageType);
    }

    [Fact]
    public void ProviderResult_Succeeded_ShouldSetSuccessAndData()
    {
        var data = new { Temp = 22 };
        var source = new SourceReference { Title = "Test Source", SourceName = "TestSource" };
        var result = ProviderResult.Succeeded("p1", "Provider 1", data, 0.95, new[] { source });

        Assert.True(result.Success);
        Assert.Equal("p1", result.ProviderId);
        Assert.Equal(0.95, result.Confidence);
        Assert.Single(result.Sources);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ProviderResult_Failed_ShouldSetFailureAndError()
    {
        var result = ProviderResult.Failed("p1", "Provider 1", "Timeout");

        Assert.False(result.Success);
        Assert.Equal(0.0, result.Confidence);
        Assert.Equal("Timeout", result.Error);
    }

    [Fact]
    public void ToolExecutionResult_AwaitingApproval_ShouldFlagRequiredApproval()
    {
        var result = ToolExecutionResult.AwaitingApproval("file_system", "Approval needed to delete file");

        Assert.False(result.Success);
        Assert.True(result.RequiredApproval);
        Assert.Contains("Approval needed", result.Output);
    }
}
