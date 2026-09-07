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
        }

        _sessionCts?.Cancel();

        var session = _sessionStore.Get(_sessionId);
        if (session != null)
        {
            session.EndedAt = DateTimeOffset.UtcNow;
            session.CurrentState = VoiceState.Idle;
        }

        CurrentState = VoiceState.Idle;
        VoiceEventEmitted?.Invoke("voice.session.stopped", new { SessionId = _sessionId });

        return Task.CompletedTask;
    }

    public Task InterruptAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        if (_currentTurn != null)
        {
            _currentTurn.MarkInterrupted();
        }

        var session = _sessionStore.Get(_sessionId);
        if (session != null)
        {
            session.InterruptionCount++;
            session.CurrentState = VoiceState.Interrupted;
        }

        CurrentState = VoiceState.Interrupted;
        VoiceEventEmitted?.Invoke("voice.interrupted", new
        {
            TurnId = _currentTurn?.TurnId,
            InterruptionLatencyMs = sw.Elapsed.TotalMilliseconds
        });

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

    public async Task<VoiceTurnResult> ProcessUtteranceAsync(string text, bool isInterruption = false, CancellationToken cancellationToken = default)
    {
        var totalSw = Stopwatch.StartNew();

        // Cancel-and-replace: if a previous turn is in progress, interrupt and cancel it immediately
        var priorTurn = _currentTurn;
        if (priorTurn != null && !priorTurn.IsCompleted && !priorTurn.TurnCts.IsCancellationRequested)
        {
            priorTurn.MarkInterrupted();
            try
            {
                priorTurn.TurnCts.Cancel();
            }
            catch (ObjectDisposedException) { }
        }

        var turnContext = new VoiceTurnContext
        {
            UserUtterance = text.Trim(),
            IsInterrupted = isInterruption
        };

        _currentTurn = turnContext;
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            turnContext.TurnCts.Token,
            _sessionCts?.Token ?? CancellationToken.None);

        var session = _sessionStore.GetOrCreate(_sessionId, _conversationId);
        session.TurnsCount++;
        session.LastUserUtterance = text;

        var cleanText = text.Trim();

        // 1. Check Voice Control Commands
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

            VoiceEventEmitted?.Invoke("voice.speaking", new { Message = repeatSpeech });
            foreach (var chunk in chunks)
            {
                VoiceEventEmitted?.Invoke("voice.speech.chunk", new { Chunk = chunk });
            }

            turnContext.MarkCompleted(session.LastAssistantResponse ?? repeatSpeech, repeatSpeech);

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

        // 2. State Transition: DetectingSpeech -> Processing
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

            if (linkedCts.IsCancellationRequested || turnContext.IsInterrupted || _currentTurn != turnContext)
            {
                turnContext.MarkInterrupted();
                return new VoiceTurnResult
                {
                    TurnId = turnContext.TurnId,
                    UserUtterance = cleanText,
                    WasInterrupted = true,
                    AssistantResponse = response.Message
                };
            }

            // 3. Conversational Speech Formatting
            var voiceFriendly = response.VoiceFriendlyMessage
                ?? _responseFormatter.FormatForSpeech(response.Message);

            var chunks = _responseFormatter.ChunkForStreamingTts(voiceFriendly);

            session.LastAssistantResponse = response.Message;
            session.LastVoiceFriendlyResponse = voiceFriendly;

            // 4. State Transition: Speaking (only if still active turn)
            if (_currentTurn == turnContext)
            {
                CurrentState = VoiceState.Speaking;
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
                    break;
                }
                VoiceEventEmitted?.Invoke("voice.speech.chunk", new { Chunk = chunk });
            }

            turnContext.MarkCompleted(response.Message, voiceFriendly);

            var telemetry = new VoiceTelemetryRecord
            {
                SessionId = _sessionId,
                TurnId = turnContext.TurnId,
                TotalTurnDurationMs = totalSw.Elapsed.TotalMilliseconds,
                ProviderLatencyMs = providerMs
            };
            TelemetryRecorded?.Invoke(telemetry);

            // 5. State Transition: Loop back to Listening for Full-Duplex Continuous Conversation
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
}
