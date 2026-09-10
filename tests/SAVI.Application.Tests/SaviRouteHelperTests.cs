using SAVI.Application.Common;
using Xunit;

namespace SAVI.Application.Tests;

public class SaviRouteHelperTests
{
    [Theory]
    [InlineData("chat/d5d16f4b-62a8-4685-bb49-ba8a8caec3b4", "d5d16f4b-62a8-4685-bb49-ba8a8caec3b4")]
    [InlineData("/chat/ABC", "ABC")]
    [InlineData("voice/d5d16f4b-62a8-4685-bb49-ba8a8caec3b4", "d5d16f4b-62a8-4685-bb49-ba8a8caec3b4")]
    [InlineData("/voice/XYZ", "XYZ")]
    [InlineData("https://savi.ai/chat/conv-123?debug=true#title", "conv-123")]
    [InlineData("https://savi.ai/voice/conv-456?mode=fast", "conv-456")]
    [InlineData("/chat", null)]
    [InlineData("/voice", null)]
    [InlineData("/tasks", null)]
    [InlineData("/memory", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ExtractConversationId_ShouldParseCorrectly(string? uri, string? expectedId)
    {
        var result = SaviRouteHelper.ExtractConversationId(uri);
        Assert.Equal(expectedId, result);
    }

    [Theory]
    [InlineData("/voice", true)]
    [InlineData("/voice/ABC", true)]
    [InlineData("voice/ABC", true)]
    [InlineData("https://savi.ai/voice/123", true)]
    [InlineData("/chat", false)]
    [InlineData("/chat/ABC", false)]
    [InlineData("/tasks", false)]
    [InlineData("", false)]
    public void IsVoiceRoute_ShouldDetectCorrectly(string? uri, bool expected)
    {
        var result = SaviRouteHelper.IsVoiceRoute(uri);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("/chat", true)]
    [InlineData("/chat/ABC", true)]
    [InlineData("chat/ABC", true)]
    [InlineData("", true)]
    [InlineData("/", true)]
    [InlineData("/voice", false)]
    [InlineData("/voice/ABC", false)]
    public void IsChatRoute_ShouldDetectCorrectly(string? uri, bool expected)
    {
        var result = SaviRouteHelper.IsChatRoute(uri);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("conv-123", "/voice/conv-123")]
    [InlineData("", "/voice")]
    [InlineData(null, "/voice")]
    public void BuildVoiceRoute_ShouldFormatCorrectly(string? id, string expected)
    {
        var result = SaviRouteHelper.BuildVoiceRoute(id);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("conv-123", "/chat/conv-123")]
    [InlineData("", "/chat")]
    [InlineData(null, "/chat")]
    public void BuildChatRoute_ShouldFormatCorrectly(string? id, string expected)
    {
        var result = SaviRouteHelper.BuildChatRoute(id);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void BuildToggleModeRoute_ChatToVoice_PreservesConversation()
    {
        var target = SaviRouteHelper.BuildToggleModeRoute("/chat/conv-999", null);
        Assert.Equal("/voice/conv-999", target);
    }

    [Fact]
    public void BuildToggleModeRoute_VoiceToChat_PreservesConversation()
    {
        var target = SaviRouteHelper.BuildToggleModeRoute("/voice/conv-999", null);
        Assert.Equal("/chat/conv-999", target);
    }

    [Fact]
    public void BuildToggleModeRoute_ExplicitActiveId_Overrides()
    {
        var target = SaviRouteHelper.BuildToggleModeRoute("/tasks", "active-conv");
        Assert.Equal("/voice/active-conv", target);
    }
}
