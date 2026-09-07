using System.Collections.Concurrent;
using SAVI.Core.Models;

namespace SAVI.Infrastructure.Voice;

public class VoiceSessionStore
{
    private readonly ConcurrentDictionary<string, VoiceSession> _sessions = new();

    public VoiceSession GetOrCreate(string sessionId, string conversationId)
    {
        return _sessions.GetOrAdd(sessionId, id => new VoiceSession
        {
            SessionId = id,
            ConversationId = conversationId
        });
    }

    public VoiceSession? Get(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    public void Remove(string sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
    }
}
