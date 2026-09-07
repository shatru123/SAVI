using System.Text;
using SAVI.Core.Interfaces;

namespace SAVI.Infrastructure.Speech;

public class SystemSpeechService : ISpeechService
{
    public Task<string> RecognizeSpeechAsync(Stream audioStream, CancellationToken cancellationToken = default)
    {
        // Fallback STT abstraction; web UI uses Web Speech API directly in browser
        return Task.FromResult("Speech recognition processed via browser speech engine.");
    }

    public Task<byte[]> SynthesizeSpeechAsync(string text, string? voice = null, CancellationToken cancellationToken = default)
    {
        // Fallback TTS abstraction; web UI uses Web Speech Synthesis API directly in browser
        return Task.FromResult(Encoding.UTF8.GetBytes(text));
    }
}
