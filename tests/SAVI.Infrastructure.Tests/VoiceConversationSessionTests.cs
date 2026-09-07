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
        var detected = false;

        vad.SpeechStarted += () => started = true;
        vad.SpeechDetected += _ => detected = true;
        vad.SpeechStopped += () => stopped = true;

        // Frame with high level
        vad.ProcessAudioFrame(0.35);
        Assert.True(started);
        Assert.True(detected);
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

    [Fact]
    public void ScenarioA_NaturalTurnTaking_Flow()
    {
        var detector = new IntentDetector();
        var personality = new SAVI.Agent.Personality.PersonalityEngine();

        // Step 1: User asks check prompt
        var intent1 = detector.Detect("SAVI, can you check something for me?");
        Assert.Equal("chitchat", intent1.Capability);
        Assert.Equal("check_prompt", intent1.Operation);

        var reply1 = personality.FormatChitChat(intent1.Operation, "SAVI, can you check something for me?");
        Assert.Equal("Sure, what do you need?", reply1);

        // Step 2: User asks weather for Tokyo
        var intent2 = detector.Detect("What's the weather like in Tokyo right now?");
        Assert.Equal(SaviConstants.Capabilities.Weather, intent2.Capability);
        Assert.Equal("current", intent2.Operation);
        Assert.Equal("Tokyo", intent2.Parameters["city"]);
    }

    [Fact]
    public void ScenarioB_ConversationalBargeIn_And_BrevityModifier()
    {
        var detector = new IntentDetector();

        // 1. User interrupts with brevity command
        var brevityIntent = detector.Detect("Wait, keep it short.");
        Assert.Equal("voice_control", brevityIntent.Capability);
        Assert.Equal("keep_it_short", brevityIntent.Operation);

        var brevityIntent2 = detector.Detect("keep it short");
        Assert.Equal("voice_control", brevityIntent2.Capability);
        Assert.Equal("keep_it_short", brevityIntent2.Operation);

        // 2. Test orchestrator brevity resolution for .NET dependency injection
        var context = new ContextPackage
        {
            CurrentPrompt = "Wait, keep it short.",
            RecentMessages = new List<Message>
            {
                new() { Role = MessageRole.User, Content = "Explain how .NET dependency injection works under the hood." },
                new() { Role = MessageRole.Assistant, Content = "In .NET, dependency injection is built around the ServiceProvider and ServiceCollection types. When you register a service..." }
            }
        };

        var method = typeof(SAVI.Agent.Orchestration.AgentOrchestrator)
            .GetMethod("HandleKeepItShort", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var response = (string)method.Invoke(null, new object[] { context })!;
        Assert.Contains("three lifetimes", response);
        Assert.Contains("Transient", response);
        Assert.Contains("Scoped", response);
        Assert.Contains("Singleton", response);
    }

    [Fact]
    public void ScenarioC_SelfCorrection_And_NeverMind()
    {
        var detector = new IntentDetector();
        var personality = new SAVI.Agent.Personality.PersonalityEngine();

        // Isolated "Actually never mind"
        var neverMindIntent = detector.Detect("Actually never mind");
        Assert.Equal("chitchat", neverMindIntent.Capability);
        Assert.Equal("never_mind", neverMindIntent.Operation);
        Assert.Equal("No worries.", personality.FormatChitChat(neverMindIntent.Operation, "Actually never mind"));

        // "Actually never mind, what about Berlin?" with prior context of Paris population
        var context = new ContextPackage
        {
            CurrentPrompt = "Actually never mind, what about Berlin?",
            RecentMessages = new List<Message>
            {
                new() { Role = MessageRole.User, Content = "What's the population of Paris?" },
                new() { Role = MessageRole.Assistant, Content = "The population of Paris is approximately 2.1 million within city limits..." }
            }
        };

        var followup = detector.Detect("Actually never mind, what about Berlin?", context);
        Assert.Equal(SaviConstants.Capabilities.Knowledge, followup.Capability);
        Assert.Equal("summary", followup.Operation);
        Assert.Equal("population of Berlin", followup.Parameters["topic"]);
    }

    [Fact]
    public void ScenarioD_Clarification_WhatNote()
    {
        var detector = new IntentDetector();

        var clarifyIntent = detector.Detect("Wait, what note?");
        Assert.Equal("voice_control", clarifyIntent.Capability);
        Assert.Equal("clarify_note", clarifyIntent.Operation);

        var context = new ContextPackage
        {
            CurrentPrompt = "Wait, what note?",
            RecentMessages = new List<Message>
            {
                new() { Role = MessageRole.Assistant, Content = "Would you like me to save that note or send it?" }
            }
        };

        var method = typeof(SAVI.Agent.Orchestration.AgentOrchestrator)
            .GetMethod("HandleClarifyNote", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var noteMsg = new ContextPackage
        {
            RecentMessages = new List<Message>
            {
                new() { Role = MessageRole.Assistant, Content = "I have a note about your meeting at 3 PM. Would you like me to save that note or send it?" }
            }
        };

        var response = (string)method.Invoke(null, new object[] { noteMsg })!;
        Assert.Equal("The note about your meeting at 3 PM.", response);
    }

    [Fact]
    public void ScenarioE_Continuation_And_GoBack()
    {
        var detector = new IntentDetector();
        var personality = new SAVI.Agent.Personality.PersonalityEngine();

        // "Wait" -> "Yep?"
        var waitIntent = detector.Detect("Wait");
        Assert.Equal("voice_control", waitIntent.Capability);
        Assert.Equal("wait", waitIntent.Operation);
        Assert.Equal("Yep?", personality.FormatChitChat(waitIntent.Operation, "Wait"));

        // "Go back" -> "Sure."
        var goBackIntent = detector.Detect("Go back");
        Assert.Equal("voice_control", goBackIntent.Capability);
        Assert.Equal("go_back", goBackIntent.Operation);
        Assert.Equal("Sure.", personality.FormatChitChat(goBackIntent.Operation, "Go back"));

        // "Actually, that's not what I meant" -> "Got it. What did you mean?"
        var clarifyIntent = detector.Detect("Actually, that's not what I meant");
        Assert.Equal("chitchat", clarifyIntent.Capability);
        Assert.Equal("clarify", clarifyIntent.Operation);
        Assert.Equal("Got it. What did you mean?", personality.FormatChitChat(clarifyIntent.Operation, "Actually, that's not what I meant"));

        // "Go back to the first one" for Mars facts
        var firstItemIntent = detector.Detect("Go back to the first one.");
        Assert.Equal("voice_control", firstItemIntent.Capability);
        Assert.Equal("first_item", firstItemIntent.Operation);

        var marsContext = new ContextPackage
        {
            RecentMessages = new List<Message>
            {
                new() { Role = MessageRole.Assistant, Content = "Fact one: Mars is known as the Red Planet due to iron oxide on its surface. Fact two..." }
            }
        };

        var method = typeof(SAVI.Agent.Orchestration.AgentOrchestrator)
            .GetMethod("HandleFirstItem", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var result = (string)method.Invoke(null, new object[] { marsContext })!;
        Assert.Equal("Mars gets its red color from iron oxide, or rust, covering its surface.", result);
    }

    [Fact]
    public void VoiceTelemetryRecord_TracksHighPrecisionLatencies()
    {
        var telemetry = new VoiceTelemetryRecord
        {
            TurnId = "turn_123",
            SessionId = "session_456",
            SttPartialLatencyMs = 45.2,
            SttFinalLatencyMs = 110.5,
            TtsStartLatencyMs = 85.0,
            AudioStopLatencyMs = 18.0,
            InterruptionDetectionLatencyMs = 12.0,
            InterruptedTurn = true
        };

        Assert.Equal(45.2, telemetry.SttPartialLatencyMs);
        Assert.Equal(18.0, telemetry.AudioStopLatencyMs);
        Assert.True(telemetry.TotalInterruptionLatencyMs <= 200, "Barge-in latency must be under 200ms target");
        Assert.True(telemetry.InterruptedTurn);
    }

    [Fact]
    public async Task ProcessUtteranceAsync_CancelAndReplace_CancelsPriorTurn_AndExecutesNewTurn()
    {
        await _session.StartAsync("conv_123");

        var turn1Tcs = new TaskCompletionSource<AgentResponse>();
        _orchestrator.Handler = (req, ct) =>
        {
            if (req.Message.Contains("long question"))
            {
                ct.Register(() => turn1Tcs.TrySetCanceled());
                return turn1Tcs.Task;
            }

            return Task.FromResult(new AgentResponse
            {
                Message = "Turn 2 completed response",
                VoiceFriendlyMessage = "Turn 2 voice response",
                Success = true,
                ConversationId = "conv_123"
            });
        };

        // Start Turn 1 (long running)
        var turn1Task = _session.ProcessUtteranceAsync("This is a long question that gets interrupted");

        // User speaks Turn 2 while Turn 1 is running
        await Task.Delay(20);
        var turn2Result = await _session.ProcessUtteranceAsync("Wait, what about the weather in Pune?");

        var turn1Result = await turn1Task;

        // Verify Turn 1 was cancelled/interrupted
        Assert.True(turn1Result.WasInterrupted);

        // Verify Turn 2 succeeded seamlessly
        Assert.True(turn2Result.Success);
        Assert.Equal("Turn 2 completed response", turn2Result.AssistantResponse);
        Assert.Equal("Turn 2 voice response", turn2Result.VoiceFriendlyResponse);
        Assert.Equal(VoiceState.Listening, _session.CurrentState);
    }

    [Fact]
    public void AudioRingBuffer_CircularBuffering_And_PreRollExtraction()
    {
        var ringBuffer = new AudioRingBuffer(capacityBytes: 100);

        // Test writing bytes
        var sampleBytes = new byte[60];
        for (int i = 0; i < 60; i++) sampleBytes[i] = (byte)i;
        ringBuffer.Write(sampleBytes);
        Assert.Equal(60, ringBuffer.AvailableBytes);

        // Write more bytes to force circular wrap
        var moreBytes = new byte[70];
        for (int i = 0; i < 70; i++) moreBytes[i] = (byte)(100 + i);
        ringBuffer.Write(moreBytes);
        Assert.Equal(100, ringBuffer.AvailableBytes);

        var available = ringBuffer.ReadAvailable();
        Assert.Equal(100, available.Length);
        Assert.Equal(169, available[^1]);

        // Test float pre-roll extraction (16kHz, 10ms = 160 samples = 320 bytes)
        var audioBuffer = new AudioRingBuffer(capacityBytes: 4096);
        var floatSamples = new float[500];
        for (int i = 0; i < 500; i++) floatSamples[i] = 0.5f;
        audioBuffer.Write(floatSamples);

        // Extract 10ms at 16kHz = 160 samples
        var preRoll = audioBuffer.ReadPreRoll(durationMs: 10, sampleRate: 16000);
        Assert.Equal(160, preRoll.Length);
        Assert.InRange(preRoll[0], 0.49f, 0.51f);

        // Test clear
        audioBuffer.Clear();
        Assert.Equal(0, audioBuffer.AvailableBytes);
    }

    [Fact]
    public async Task AcceptanceTest1_WeatherInterruption_FollowUpWithInterruptedContext()
    {
        await _session.StartAsync("conv_weather");
        var detector = new IntentDetector();

        // 1. Turn 1: Weather in Pune
        _orchestrator.Handler = (req, ct) => Task.FromResult(new AgentResponse
        {
            Message = "Sure. It's currently around 28 degrees and tomorrow—",
            VoiceFriendlyMessage = "Sure. It's currently around 28 degrees and tomorrow—",
            Success = true,
            ConversationId = "conv_weather"
        });

        var turn1 = await _session.ProcessUtteranceAsync("Hey SAVI, tell me the weather in Pune.");
        Assert.Equal(VoiceState.Listening, _session.CurrentState);

        // 2. Turn 2: User interrupts with "Wait, what about Mumbai?"
        var weatherContext = new ContextPackage
        {
            CurrentPrompt = "Wait, what about Mumbai?",
            RecentMessages = new List<Message>
            {
                new() { Role = MessageRole.User, Content = "Hey SAVI, tell me the weather in Pune.", Timestamp = DateTimeOffset.UtcNow.AddSeconds(-5) },
                new() { Role = MessageRole.Assistant, Content = "Sure. It's currently around 28 degrees and tomorrow—", Timestamp = DateTimeOffset.UtcNow.AddSeconds(-2) }
            }
        };

        var intent = detector.Detect("Wait, what about Mumbai?", weatherContext);
        Assert.Equal(SaviConstants.Capabilities.Weather, intent.Capability);
        Assert.Equal("Mumbai", intent.Parameters["city"]);

        _orchestrator.Handler = (req, ct) => Task.FromResult(new AgentResponse
        {
            Message = "In Mumbai, it is currently 31 degrees and humid.",
            VoiceFriendlyMessage = "In Mumbai, it's 31 degrees right now.",
            Success = true,
            ConversationId = "conv_weather"
        });

        var turn2 = await _session.ProcessUtteranceAsync("Wait, what about Mumbai?", isInterruption: true);
        Assert.True(turn2.Success);
        Assert.Equal("In Mumbai, it's 31 degrees right now.", turn2.VoiceFriendlyResponse);
    }

    [Fact]
    public void AcceptanceTest2_NoIMeantSomethingElse_CapturedAsSingleTurn()
    {
        var detector = new IntentDetector();
        var personality = new SAVI.Agent.Personality.PersonalityEngine();

        // User speaks: "No, I meant something else."
        var intent = detector.Detect("No, I meant something else.");
        Assert.Equal("chitchat", intent.Capability);
        Assert.Equal("clarify", intent.Operation);

        var reply = personality.FormatChitChat(intent.Operation, "No, I meant something else.");
        Assert.Equal("Got it. What did you mean?", reply);
    }

    [Fact]
    public async Task ProcessUtteranceAsync_StaleResultDiscarding_LateTurn1ResultNeverOverwritesTurn2()
    {
        await _session.StartAsync("conv_stale");

        var turn1StartedTcs = new TaskCompletionSource();
        var turn1FinishTcs = new TaskCompletionSource<AgentResponse>();

        _orchestrator.Handler = async (req, ct) =>
        {
            if (req.Message.Contains("Turn 1 slow"))
            {
                turn1StartedTcs.TrySetResult();
                return await turn1FinishTcs.Task;
            }

            return new AgentResponse
            {
                Message = "Turn 2 fast response",
                VoiceFriendlyMessage = "Turn 2 fast response",
                Success = true,
                ConversationId = "conv_stale"
            };
        };

        // Start Turn 1 (slow)
        var turn1Task = _session.ProcessUtteranceAsync("Turn 1 slow prompt");
        await turn1StartedTcs.Task;

        // Turn 2 arrives while Turn 1 is still waiting on orchestrator
        var turn2Result = await _session.ProcessUtteranceAsync("Turn 2 prompt");

        Assert.Equal("Turn 2 fast response", turn2Result.AssistantResponse);
        Assert.True(turn2Result.Success);

        // Turn 1 finally finishes late
        turn1FinishTcs.TrySetResult(new AgentResponse
        {
            Message = "Late Turn 1 response",
            VoiceFriendlyMessage = "Late Turn 1 response",
            Success = true,
            ConversationId = "conv_stale"
        });

        var turn1Result = await turn1Task;

        // Turn 1 was superseded, so its result was discarded/marked interrupted
        Assert.True(turn1Result.WasInterrupted);

        // The session store still remembers Turn 2 as the active response
        var stored = _store.Get(_session.SessionId);
        Assert.NotNull(stored);
        Assert.Equal("Turn 2 fast response", stored.LastAssistantResponse);
    }

    [Fact]
    public async Task SelfEcho_TranscriptsMatchingAssistantSpeech_AreDiscardedWithoutCreatingTurns()
    {
        await _session.StartAsync("conv_echo");
        var stored = _store.GetOrCreate(_session.SessionId, "conv_echo");
        stored.LastVoiceFriendlyResponse = "The weather in Pune today is around 28 degrees with clear skies.";
        stored.AudioOutputMode = "speaker";

        // Microphone captures SAVI's own speaker output
        var echoUtterance = "The weather in Pune today is around 28 degrees";
        var result = await _session.ProcessUtteranceAsync(echoUtterance);

        Assert.Equal("Self-echo suppressed.", result.AssistantResponse);
        Assert.Equal(1, stored.SelfEchoSuppressedCount);
        Assert.Equal(0, _orchestrator.CallCount); // Orchestrator is never invoked for acoustic echoes!
    }

    [Fact]
    public async Task BargeIn_UserSpeechDifferingFromAssistantSpeech_IsProcessedNormally()
    {
        await _session.StartAsync("conv_barge");
        var stored = _store.GetOrCreate(_session.SessionId, "conv_barge");
        stored.LastVoiceFriendlyResponse = "The weather in Pune today is around 28 degrees with clear skies.";
        stored.AudioOutputMode = "speaker";

        _orchestrator.Handler = (req, ct) => Task.FromResult(new AgentResponse
        {
            Message = "In Mumbai it is 31 degrees.",
            VoiceFriendlyMessage = "In Mumbai it is 31 degrees.",
            Success = true,
            ConversationId = "conv_barge"
        });

        // User speaks a true barge-in with different content
        var bargeInUtterance = "Wait, what about Mumbai?";
        var result = await _session.ProcessUtteranceAsync(bargeInUtterance, isInterruption: true);

        Assert.True(result.Success);
        Assert.Equal("In Mumbai it is 31 degrees.", result.AssistantResponse);
        Assert.Equal(1, _orchestrator.CallCount);
        Assert.Equal(0, stored.SelfEchoSuppressedCount);
    }

    [Fact]
    public void IsSelfEcho_Algorithm_CorrectlyDistinguishesEchosFromBargeIn()
    {
        var assistant = "The weather in Pune today is around 28 degrees with clear skies.";

        // Substring / substantial overlap echo
        Assert.True(VoiceConversationSession.IsSelfEcho("The weather in Pune today is around 28 degrees", assistant));
        Assert.True(VoiceConversationSession.IsSelfEcho("around 28 degrees with clear skies", assistant));
        Assert.True(VoiceConversationSession.IsSelfEcho("weather in pune today is 28 degrees", assistant));

        // True user barge-in commands (must NEVER be classified as echo)
        Assert.False(VoiceConversationSession.IsSelfEcho("Wait, what about Mumbai?", assistant));
        Assert.False(VoiceConversationSession.IsSelfEcho("Stop, that's enough.", assistant));
        Assert.False(VoiceConversationSession.IsSelfEcho("Actually, check tomorrow instead.", assistant));
        Assert.False(VoiceConversationSession.IsSelfEcho("No, I meant something else.", assistant));

        // Unrelated speech
        Assert.False(VoiceConversationSession.IsSelfEcho("Can you write a python script for sorting an array?", assistant));
    }
}
