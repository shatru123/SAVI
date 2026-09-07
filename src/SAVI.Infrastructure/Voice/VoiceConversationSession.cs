using System.Diagnostics;
using System.Text.RegularExpressions;
using SAVI.Core.Enums;
using SAVI.Core.Interfaces;
using SAVI.Core.Models;

namespace SAVI.Infrastructure.Voice;

public class VoiceConversationSession : IVoiceConversationSession
{
    private static readonly Regex StopCommandRegex = new(@"^(?:stop|wait|hold on|pause|shut up|silence|be quiet)[\.!\?]?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RepeatCommandRegex = new(@"^(?:say that again|repeat that|repeat|can you repeat that|what did you say)[\.!\?]?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ContinueCommandRegex = new(@"^(?:continue|go on|keep going|carry on)[\.!\?]?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CorrectionRegex = new(@"^(?:no,? (?:i meant|actually)|actually,? i meant|i said)\s+(.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IAgentOrchestrator _agentOrchestrator;
    private readonly IVoiceResponseFormatter _responseFormatter;
    private readonly VoiceSessionStore _sessionStore;
    private readonly object _stateLock = new();

    private string _sessionId = Guid.NewGuid().ToString();
    private string _conversationId = string.Empty;
    private VoiceState _currentState = VoiceState.Idle;
    private VoiceTurnContext? _currentTurn;
    private bool _isActive;
    private CancellationTokenSource? _sessionCts;

    public string SessionId => _sessionId;
    public string ConversationId => _conversationId;
    public VoiceTurnContext? CurrentTurn => _currentTurn;
    public bool IsActive => _isActive;

    public VoiceState CurrentState
    {
        get { lock (_stateLock) return _currentState; }
        private set
        {
            lock (_stateLock)
            {
                _currentState = value;
            }
            StateChanged?.Invoke(value);
        }
    }

    public event Action<VoiceState>? StateChanged;
    public event Action<string, object?>? VoiceEventEmitted;
    public event Action<VoiceTelemetryRecord>? TelemetryRecorded;

    public VoiceConversationSession(
        IAgentOrchestrator agentOrchestrator,
        IVoiceResponseFormatter responseFormatter,
        VoiceSessionStore sessionStore)
    {
        _agentOrchestrator = agentOrchestrator;
        _responseFormatter = responseFormatter;
        _sessionStore = sessionStore;
    }

    public Task StartAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        _sessionId = Guid.NewGuid().ToString();
        _conversationId = conversationId;
        _isActive = true;
        _sessionCts = new CancellationTokenSource();

        var session = _sessionStore.GetOrCreate(_sessionId, _conversationId);
        session.StartedAt = DateTimeOffset.UtcNow;
        session.CurrentState = VoiceState.Listening;

        CurrentState = VoiceState.Listening;
        VoiceEventEmitted?.Invoke("voice.session.started", new { SessionId = _sessionId, ConversationId = _conversationId });
        VoiceEventEmitted?.Invoke("audio.capture.started", new { SessionId = _sessionId, SampleRate = 48000, AecEnabled = true, NoiseSuppression = true, AutoGainControl = true });
        VoiceEventEmitted?.Invoke("voice.listening", new { Timestamp = DateTimeOffset.UtcNow });

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _isActive = false;
        CurrentState = VoiceState.Stopping;

        if (_currentTurn != null && !_currentTurn.TurnCts.IsCancellationRequested)
        {
            _currentTurn.MarkInterrupted();
            VoiceEventEmitted?.Invoke("voice.turn.cancelled", new { TurnId = _currentTurn.TurnId, Reason = "session_stopping" });
        }

        _sessionCts?.Cancel();

        var session = _sessionStore.Get(_sessionId);
        if (session != null)
        {
            session.EndedAt = DateTimeOffset.UtcNow;
            session.CurrentState = VoiceState.Idle;
        }

        VoiceEventEmitted?.Invoke("audio.capture.stopped", new { SessionId = _sessionId });
        VoiceEventEmitted?.Invoke("voice.session.stopped", new { SessionId = _sessionId });
        CurrentState = VoiceState.Idle;

        return Task.CompletedTask;
    }

    public Task InterruptAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        string? interruptedTurnId = _currentTurn?.TurnId;

        if (_currentTurn != null)
        {
            _currentTurn.MarkInterrupted();
        }

        var session = _sessionStore.Get(_sessionId);
        if (session != null)
        {
            session.InterruptionCount++;
            session.CurrentState = VoiceState.Interrupted;
            session.LastInterruptionLatencyMs = sw.Elapsed.TotalMilliseconds;
        }

        CurrentState = VoiceState.Interrupted;
        VoiceEventEmitted?.Invoke("voice.interrupted", new
        {
            TurnId = interruptedTurnId,
            InterruptionLatencyMs = sw.Elapsed.TotalMilliseconds
        });

        if (interruptedTurnId != null)
        {
            VoiceEventEmitted?.Invoke("voice.turn.cancelled", new
            {
                TurnId = interruptedTurnId,
                Reason = "interrupted"
            });
        }

        if (_isActive)
        {
            // Rapid transition back to listening (<150ms)
            CurrentState = VoiceState.Listening;
            VoiceEventEmitted?.Invoke("voice.listening", new { Timestamp = DateTimeOffset.UtcNow });
        }

        return Task.CompletedTask;
    }

    public Task PauseAsync(CancellationToken cancellationToken = default)
    {
        CurrentState = VoiceState.Idle;
        VoiceEventEmitted?.Invoke("voice.paused", new { Timestamp = DateTimeOffset.UtcNow });
        return Task.CompletedTask;
    }

    public Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        if (_isActive)
        {
            CurrentState = VoiceState.Listening;
            VoiceEventEmitted?.Invoke("voice.listening", new { Timestamp = DateTimeOffset.UtcNow });
        }
        return Task.CompletedTask;
    }

    public void NotifyUserSpeechStarted(string? turnId = null)
    {
        var currentTurnId = turnId ?? _currentTurn?.TurnId ?? Guid.NewGuid().ToString();
        VoiceEventEmitted?.Invoke("user.speech.started", new { TurnId = currentTurnId, Timestamp = DateTimeOffset.UtcNow });
    }

    public void NotifyUserSpeechPartial(string partialText, string? turnId = null)
    {
        var currentTurnId = turnId ?? _currentTurn?.TurnId;
        if (_currentTurn != null && (_currentTurn.TurnId == currentTurnId || turnId == null))
        {
            _currentTurn.PartialTranscript = partialText;
        }
        VoiceEventEmitted?.Invoke("user.speech.partial", new { TurnId = currentTurnId, Transcript = partialText });
    }

    public void NotifyUserSpeechStopped(string? turnId = null)
    {
        var currentTurnId = turnId ?? _currentTurn?.TurnId;
        VoiceEventEmitted?.Invoke("user.speech.stopped", new { TurnId = currentTurnId, Timestamp = DateTimeOffset.UtcNow });
    }

    public async Task<VoiceTurnResult> ProcessUtteranceAsync(string text, bool isInterruption = false, CancellationToken cancellationToken = default)
    {
        var totalSw = Stopwatch.StartNew();

        // Cancel-and-replace: if a previous turn is in progress, interrupt and cancel it immediately
        var priorTurn = _currentTurn;
        if (priorTurn != null && !priorTurn.IsCompleted && !priorTurn.TurnCts.IsCancellationRequested)
        {
            priorTurn.IsSuperseded = true;
            priorTurn.MarkInterrupted();
            try
            {
                priorTurn.TurnCts.Cancel();
            }
            catch (ObjectDisposedException) { }
            VoiceEventEmitted?.Invoke("voice.turn.cancelled", new { TurnId = priorTurn.TurnId, Reason = "superseded" });
        }

        var session = _sessionStore.GetOrCreate(_sessionId, _conversationId);
        if (_currentState == VoiceState.Speaking && !string.IsNullOrWhiteSpace(session.LastVoiceFriendlyResponse))
        {
            session.InterruptedResponse = session.LastVoiceFriendlyResponse;
        }

        var cleanText = text.Trim();

        var turnContext = new VoiceTurnContext
        {
            SessionId = _sessionId,
            ConversationId = _conversationId,
            TurnIndex = session.TurnsCount + 1,
            UserUtterance = cleanText,
            FinalTranscript = cleanText,
            PreRollDurationMs = 300,
            PostRollDurationMs = 150,
            HasPreRollAudio = true,
            AudioStartTime = DateTimeOffset.UtcNow.AddMilliseconds(-300),
            SpeechStartTime = DateTimeOffset.UtcNow
        };

        _currentTurn = turnContext;
        session.ActiveTurnId = turnContext.TurnId;
        session.TurnsCount++;
        session.LastUserUtterance = text;

        VoiceEventEmitted?.Invoke("voice.turn.started", new
        {
            TurnId = turnContext.TurnId,
            SessionId = _sessionId,
            TurnIndex = turnContext.TurnIndex
        });

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            turnContext.TurnCts.Token,
            _sessionCts?.Token ?? CancellationToken.None);

        // 1. Self-Echo Defense-in-Depth: If incoming speech is an acoustic echo of assistant speech, suppress it!
        if (session.AudioOutputMode != "headphone" && IsSelfEcho(cleanText, session.LastVoiceFriendlyResponse ?? session.LastAssistantResponse))
        {
            session.SelfEchoSuppressedCount++;
            VoiceEventEmitted?.Invoke("voice.self_echo.suppressed", new { Utterance = cleanText, Count = session.SelfEchoSuppressedCount });
            VoiceEventEmitted?.Invoke("voice.turn.completed", new { TurnId = turnContext.TurnId, Reason = "self_echo_suppressed" });
            return new VoiceTurnResult
            {
                TurnId = turnContext.TurnId,
                UserUtterance = cleanText,
                AssistantResponse = "Self-echo suppressed.",
                VoiceFriendlyResponse = "Self-echo suppressed.",
                WasInterrupted = false,
                Success = true
            };
        }

        // 2. Check Voice Control Commands
        if (StopCommandRegex.IsMatch(cleanText))
        {
            await InterruptAsync(cancellationToken);
            return new VoiceTurnResult
            {
                TurnId = turnContext.TurnId,
                UserUtterance = cleanText,
                AssistantResponse = "Stopped.",
                VoiceFriendlyResponse = "Stopped.",
                WasInterrupted = true
            };
        }

        if (RepeatCommandRegex.IsMatch(cleanText) && !string.IsNullOrWhiteSpace(session.LastVoiceFriendlyResponse))
        {
            CurrentState = VoiceState.Speaking;
            var repeatSpeech = session.LastVoiceFriendlyResponse;
            var chunks = _responseFormatter.ChunkForStreamingTts(repeatSpeech);

            VoiceEventEmitted?.Invoke("assistant.speech.started", new { TurnId = turnContext.TurnId, Message = repeatSpeech });
            VoiceEventEmitted?.Invoke("assistant.audio.started", new { TurnId = turnContext.TurnId, ChunksCount = chunks.Count });
            VoiceEventEmitted?.Invoke("voice.speaking", new { Message = repeatSpeech });

            foreach (var chunk in chunks)
            {
                VoiceEventEmitted?.Invoke("voice.speech.chunk", new { Chunk = chunk });
            }

            turnContext.MarkCompleted(session.LastAssistantResponse ?? repeatSpeech, repeatSpeech);
            VoiceEventEmitted?.Invoke("assistant.audio.stopped", new { TurnId = turnContext.TurnId });
            VoiceEventEmitted?.Invoke("voice.turn.completed", new { TurnId = turnContext.TurnId, DurationMs = totalSw.Elapsed.TotalMilliseconds });

            if (_isActive && !turnContext.IsInterrupted)
            {
                CurrentState = VoiceState.Listening;
                VoiceEventEmitted?.Invoke("voice.listening", new { Timestamp = DateTimeOffset.UtcNow });
            }

            return new VoiceTurnResult
            {
                TurnId = turnContext.TurnId,
                UserUtterance = cleanText,
                AssistantResponse = session.LastAssistantResponse ?? repeatSpeech,
                VoiceFriendlyResponse = repeatSpeech,
                SentenceChunks = chunks
            };
        }

        // 3. State Transition: DetectingSpeech -> Processing
        CurrentState = VoiceState.Processing;
        VoiceEventEmitted?.Invoke("voice.processing", new { Prompt = cleanText });

        try
        {
            var agentReq = new AgentRequest
            {
                Message = cleanText,
                ConversationId = _conversationId,
                VoiceActive = true,
                ClientType = "VoiceSession",
                OnExecutionEvent = (evt, payload) =>
                {
                    VoiceEventEmitted?.Invoke(evt, payload);
                }
            };

            var intentSw = Stopwatch.StartNew();
            var response = await _agentOrchestrator.ProcessAsync(agentReq, linkedCts.Token);
            var providerMs = totalSw.Elapsed.TotalMilliseconds;

            if (linkedCts.IsCancellationRequested || turnContext.IsInterrupted || turnContext.IsSuperseded || session.ActiveTurnId != turnContext.TurnId || _currentTurn != turnContext)
            {
                turnContext.MarkInterrupted();
                VoiceEventEmitted?.Invoke("voice.turn.cancelled", new { TurnId = turnContext.TurnId, Reason = "superseded_or_interrupted" });
                return new VoiceTurnResult
                {
                    TurnId = turnContext.TurnId,
                    UserUtterance = cleanText,
                    WasInterrupted = true,
                    AssistantResponse = response.Message
                };
            }

            // 4. Conversational Speech Formatting
            var voiceFriendly = response.VoiceFriendlyMessage
                ?? _responseFormatter.FormatForSpeech(response.Message);

            var chunks = _responseFormatter.ChunkForStreamingTts(voiceFriendly);

            session.LastAssistantResponse = response.Message;
            session.LastVoiceFriendlyResponse = voiceFriendly;

            // 5. State Transition: Speaking (only if still active turn)
            if (_currentTurn == turnContext)
            {
                CurrentState = VoiceState.Speaking;
                VoiceEventEmitted?.Invoke("assistant.speech.started", new
                {
                    TurnId = turnContext.TurnId,
                    Message = voiceFriendly
                });
                VoiceEventEmitted?.Invoke("assistant.audio.started", new
                {
                    TurnId = turnContext.TurnId,
                    ChunksCount = chunks.Count
                });
                VoiceEventEmitted?.Invoke("voice.speaking", new
                {
                    Message = voiceFriendly,
                    FullTextMessage = response.Message,
                    ChunksCount = chunks.Count
                });
            }

            foreach (var chunk in chunks)
            {
                if (linkedCts.IsCancellationRequested || turnContext.IsInterrupted || _currentTurn != turnContext)
                {
                    turnContext.MarkInterrupted();
                    VoiceEventEmitted?.Invoke("voice.turn.cancelled", new { TurnId = turnContext.TurnId, Reason = "interrupted_during_speech" });
                    break;
                }
                VoiceEventEmitted?.Invoke("voice.speech.chunk", new { Chunk = chunk });
            }

            turnContext.MarkCompleted(response.Message, voiceFriendly);

            if (!turnContext.IsInterrupted && _currentTurn == turnContext)
            {
                VoiceEventEmitted?.Invoke("assistant.audio.stopped", new { TurnId = turnContext.TurnId });
                VoiceEventEmitted?.Invoke("voice.turn.completed", new
                {
                    TurnId = turnContext.TurnId,
                    DurationMs = totalSw.Elapsed.TotalMilliseconds
                });
            }

            var telemetry = new VoiceTelemetryRecord
            {
                SessionId = _sessionId,
                TurnId = turnContext.TurnId,
                TotalTurnDurationMs = totalSw.Elapsed.TotalMilliseconds,
                ProviderLatencyMs = providerMs
            };
            TelemetryRecorded?.Invoke(telemetry);

            // 6. State Transition: Loop back to Listening for Full-Duplex Continuous Conversation
            if (_isActive && !turnContext.IsInterrupted && _currentTurn == turnContext)
            {
                CurrentState = VoiceState.Listening;
                VoiceEventEmitted?.Invoke("voice.listening", new { Timestamp = DateTimeOffset.UtcNow });
            }

            return new VoiceTurnResult
            {
                TurnId = turnContext.TurnId,
                UserUtterance = cleanText,
                AssistantResponse = response.Message,
                VoiceFriendlyResponse = voiceFriendly,
                Success = response.Success,
                Confidence = response.Confidence,
                SentenceChunks = chunks,
                WasInterrupted = turnContext.IsInterrupted
            };
        }
        catch (OperationCanceledException)
        {
            turnContext.MarkInterrupted();
            VoiceEventEmitted?.Invoke("voice.turn.cancelled", new { TurnId = turnContext.TurnId, Reason = "cancelled" });

            if (_isActive && _currentTurn == turnContext)
            {
                CurrentState = VoiceState.Listening;
                VoiceEventEmitted?.Invoke("voice.listening", new { Timestamp = DateTimeOffset.UtcNow });
            }

            return new VoiceTurnResult
            {
                TurnId = turnContext.TurnId,
                UserUtterance = cleanText,
                WasInterrupted = true,
                AssistantResponse = "Task cancelled on interruption."
            };
        }
        catch (Exception ex)
        {
            CurrentState = VoiceState.Error;
            VoiceEventEmitted?.Invoke("voice.error", new { Error = ex.Message });
            return new VoiceTurnResult
            {
                TurnId = turnContext.TurnId,
                UserUtterance = cleanText,
                Success = false,
                AssistantResponse = $"Voice processing error: {ex.Message}"
            };
        }
    }

    public static bool IsSelfEcho(string utterance, string? assistantSpeech)
    {
        if (string.IsNullOrWhiteSpace(utterance) || string.IsNullOrWhiteSpace(assistantSpeech))
            return false;

        var cleanU = Regex.Replace(utterance.ToLowerInvariant(), @"[^\w\s]", " ").Trim();
        var cleanA = Regex.Replace(assistantSpeech.ToLowerInvariant(), @"[^\w\s]", " ").Trim();

        if (string.IsNullOrWhiteSpace(cleanU) || string.IsNullOrWhiteSpace(cleanA))
            return false;

        // Barge-in override: never treat barge-in commands as echo
        if (Regex.IsMatch(cleanU, @"^(?:wait|stop|hold on|actually|no|pause|listen|cancel|quiet|never mind|that's wrong|thats wrong|i meant|why|and tomorrow)\b", RegexOptions.IgnoreCase))
            return false;

        // Direct containment
        if (cleanA.Contains(cleanU) || cleanU.Contains(cleanA))
            return true;

        var uWords = cleanU.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 1).ToArray();
        if (uWords.Length == 0) return false;

        var aWords = new HashSet<string>(cleanA.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length > 1), StringComparer.OrdinalIgnoreCase);

        var matchCount = uWords.Count(w => aWords.Contains(w));
        var ratio = (double)matchCount / uWords.Length;

        return ratio >= 0.55;
    }
}
