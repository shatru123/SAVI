using SAVI.Application.DTOs;
using SAVI.Core.Enums;
using Xunit;

namespace SAVI.Api.Tests;

public class ApiContractTests
{
    [Fact]
    public void ChatRequest_Serialization_ShouldRetainValues()
    {
        var req = new SendChatMessageRequest
        {
            Message = "What is the weather?",
            ConversationId = "c123",
            PersonalityOverride = PersonalityMode.Concise,
            VoiceActive = true
        };

        Assert.Equal("What is the weather?", req.Message);
        Assert.Equal("c123", req.ConversationId);
        Assert.Equal(PersonalityMode.Concise, req.PersonalityOverride);
        Assert.True(req.VoiceActive);
    }

    [Fact]
    public void ChatResponse_Serialization_ShouldPopulateDefaults()
    {
        var resp = new ChatResponseDto
        {
            Message = "The weather is sunny.",
            Success = true,
            Confidence = 0.95
        };

        Assert.Equal("The weather is sunny.", resp.Message);
        Assert.True(resp.Success);
        Assert.Equal(0.95, resp.Confidence);
        Assert.Empty(resp.Sources);
    }

    [Fact]
    public void ChatRequest_ShouldExposeProductionPayloadLimit()
    {
        Assert.Equal(12_000, SendChatMessageRequest.MaxMessageLength);
    }
}
