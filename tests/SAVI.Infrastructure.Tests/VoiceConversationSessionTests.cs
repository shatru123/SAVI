using SAVI.Agent.Routing;
using SAVI.Core.Constants;
using SAVI.Core.Entities;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;
using SAVI.Infrastructure.Voice;
using Xunit;

namespace SAVI.Infrastructure.Tests;

public class FakeAgentOrchestrator : IAgentOrchestrator
{
    public Func<AgentRequest, CancellationToken, Task<AgentResponse>>? Handler { get; set; }
    public int CallCount { get; private set; }

    public Task<AgentResponse> ProcessAsync(AgentRequest request, CancellationToken cancellationToken = default)
    {
        CallCount++;
        if (Handler != null)
        {
            return Handler(request, cancellationToken);
        }

        return Task.FromResult(new AgentResponse
        {
            Message = "Default response",
            ConversationId = request.ConversationId
        });
    }
}

public class VoiceConversationSessionTests
{
    private readonly FakeAgentOrchestrator _orchestrator;
    private readonly VoiceResponseFormatter _formatter;
    private readonly VoiceSessionStore _store;
    private readonly VoiceConversationSession _session;

    public VoiceConversationSessionTests()
    {
        _orchestrator = new FakeAgentOrchestrator();
        _formatter = new VoiceResponseFormatter();
        _store = new VoiceSessionStore();
        _session = new VoiceConversationSession(_orchestrator, _formatter, _store);
    }

    [Fact]
    public async Task StartAsync_TransitionsFromIdleToListening()
    {
        Assert.Equal(VoiceState.Idle, _session.CurrentState);

        await _session.StartAsync("conv_123");

        Assert.Equal(VoiceState.Listening, _session.CurrentState);
        Assert.True(_session.IsActive);
        Assert.Equal("conv_123", _session.ConversationId);
    }

    [Fact]
    public async Task StopAsync_TransitionsToIdle()
    {
        await _session.StartAsync("conv_123");
        Assert.True(_session.IsActive);

        await _session.StopAsync();

        Assert.Equal(VoiceState.Idle, _session.CurrentState);
        Assert.False(_session.IsActive);
    }

    [Fact]
    public async Task InterruptAsync_CancelsActiveTurn_AndReturnsToListening()
    {
        await _session.StartAsync("conv_123");

        var interruptFired = false;
        _session.VoiceEventEmitted += (evt, _) =>
        {
            if (evt == "voice.interrupted") interruptFired = true;
        };

        await _session.InterruptAsync();

        Assert.True(interruptFired);
        Assert.Equal(VoiceState.Listening, _session.CurrentState);

        var storedSession = _store.Get(_session.SessionId);
        Assert.NotNull(storedSession);
        Assert.Equal(1, storedSession.InterruptionCount);
    }

    [Fact]
    public async Task ProcessUtteranceAsync_HandlesStopCommand_WithoutQueryingOrchestrator()
    {
        await _session.StartAsync("conv_123");

        var result = await _session.ProcessUtteranceAsync("Stop.");

        Assert.True(result.WasInterrupted);
        Assert.Equal("Stopped.", result.AssistantResponse);
        Assert.Equal(0, _orchestrator.CallCount);
    }

    [Fact]
    public async Task ProcessUtteranceAsync_HandlesRepeatCommand_ReplaysPreviousResponse()
    {
        await _session.StartAsync("conv_123");

        var stored = _store.GetOrCreate(_session.SessionId, "conv_123");
        stored.LastAssistantResponse = "It is 27 degrees in Pune.";
        stored.LastVoiceFriendlyResponse = "It is 27 degrees in Pune right now.";

        var result = await _session.ProcessUtteranceAsync("Can you repeat that?");

        Assert.Equal("It is 27 degrees in Pune right now.", result.VoiceFriendlyResponse);
        Assert.NotEmpty(result.SentenceChunks);
        Assert.Equal(0, _orchestrator.CallCount);
    }

    [Fact]
    public async Task ProcessUtteranceAsync_ExecutesAndLoopsBackToListening()
    {
        await _session.StartAsync("conv_123");

        _orchestrator.Handler = (req, ct) => Task.FromResult(new AgentResponse
        {
            Message = "Here is the calculation: 42.",
            VoiceFriendlyMessage = "The result is 42.",
            Success = true,
            Confidence = 1.0,
            ConversationId = "conv_123"
        });

        var result = await _session.ProcessUtteranceAsync("What is 6 times 7?");

        Assert.True(result.Success);
        Assert.Equal("Here is the calculation: 42.", result.AssistantResponse);
        Assert.Equal("The result is 42.", result.VoiceFriendlyResponse);
        Assert.Equal(VoiceState.Listening, _session.CurrentState);
        Assert.Equal(1, _orchestrator.CallCount);
    }

    [Fact]
    public void VoiceResponseFormatter_StripsCodeBlocks_Tables_And_Urls()
    {
        var raw = @"Here is the code:
```csharp
var x = 10;
Console.WriteLine(x);
```
Check details at https://docs.microsoft.com/dotnet.
| Name | Age |
|---|---|
| Alice | 30 |
| Bob | 25 |
Answer: Done.";

        var formatted = _formatter.FormatForSpeech(raw);

        Assert.DoesNotContain("```", formatted);
        Assert.DoesNotContain("Console.WriteLine", formatted);
        Assert.Contains("The code solution is displayed on screen.", formatted);
        Assert.DoesNotContain("https://", formatted);
        Assert.DoesNotContain("| Alice | 30 |", formatted);
        Assert.Contains("The structured data table is displayed on your screen.", formatted);
    }

    [Fact]
    public void VoiceResponseFormatter_ChunksSentencesAccurately()
    {
        var speech = "Right now it's 27 degrees in Pune. The skies are partly cloudy. Would you like the forecast for tomorrow?";
        var chunks = _formatter.ChunkForStreamingTts(speech);

        Assert.Equal(3, chunks.Count);
        Assert.Equal("Right now it's 27 degrees in Pune.", chunks[0]);
        Assert.Equal("The skies are partly cloudy.", chunks[1]);
        Assert.Equal("Would you like the forecast for tomorrow?", chunks[2]);
    }

    [Fact]
    public void VoiceActivityDetector_DetectsSpeechAndSilenceTransitions()
    {
        var vad = new VoiceActivityDetector
        {
            SpeechStartThreshold = 0.15,
            SilenceDurationThresholdMs = 50
        };

        var started = false;
        var stopped = false;

        vad.SpeechStarted += () => started = true;
        vad.SpeechStopped += () => stopped = true;

        // Frame with high level
        vad.ProcessAudioFrame(0.35);
        Assert.True(started);
        Assert.True(vad.IsSpeechActive);

        // Silence frames
        vad.ProcessAudioFrame(0.02);
        Thread.Sleep(60);
        vad.ProcessAudioFrame(0.02);

        Assert.True(stopped);
        Assert.False(vad.IsSpeechActive);
    }

    [Fact]
    public void IntentDetector_ResolvesConversationalFollowUps_And_Corrections()
    {
        var detector = new IntentDetector();

        // 1. Voice commands
        var stopIntent = detector.Detect("Stop");
        Assert.Equal("voice_control", stopIntent.Capability);
        Assert.Equal("stop", stopIntent.Operation);

        var repeatIntent = detector.Detect("Say that again");
        Assert.Equal("voice_control", repeatIntent.Capability);
        Assert.Equal("repeat", repeatIntent.Operation);

        // 2. Weather follow-up with context
        var context = new ContextPackage
        {
            CurrentPrompt = "What about tomorrow?",
            RecentMessages = new List<Message>
            {
                new() { Role = MessageRole.User, Content = "What is the weather in Pune?", Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1) },
                new() { Role = MessageRole.Assistant, Content = "Right now, Pune is 27°C.", Timestamp = DateTimeOffset.UtcNow }
            }
        };

        var followupIntent = detector.Detect("What about tomorrow?", context);
        Assert.Equal(SaviConstants.Capabilities.Weather, followupIntent.Capability);
        Assert.Equal("forecast", followupIntent.Operation);
        Assert.Equal("Pune", followupIntent.Parameters["city"]);

        // 3. Location change follow-up: "And what about Mumbai?"
        var mumbaiIntent = detector.Detect("And what about Mumbai?", context);
        Assert.Equal(SaviConstants.Capabilities.Weather, mumbaiIntent.Capability);
        Assert.Equal("Mumbai", mumbaiIntent.Parameters["city"]);

        // 4. Correction: "No, I meant Pune, not Patna"
        var correctionContext = new ContextPackage
        {
            CurrentPrompt = "No, I meant Pune, not Patna",
            RecentMessages = new List<Message>
            {
                new() { Role = MessageRole.User, Content = "What is the weather in Patna?", Timestamp = DateTimeOffset.UtcNow.AddMinutes(-1) }
            }
        };

        var correctionIntent = detector.Detect("No, I meant Pune, not Patna", correctionContext);
        Assert.Equal(SaviConstants.Capabilities.Weather, correctionIntent.Capability);
        Assert.Equal("Pune", correctionIntent.Parameters["city"]);
    }
}
