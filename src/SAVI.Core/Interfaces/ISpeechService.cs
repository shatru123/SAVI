using SAVI.Core.Enums;

namespace SAVI.Core.Interfaces;

public interface ISpeechService
{
    Task<string> RecognizeSpeechAsync(Stream audioStream, CancellationToken cancellationToken = default);
    Task<byte[]> SynthesizeSpeechAsync(string text, string? voice = null, CancellationToken cancellationToken = default);
}
