namespace SAVI.Core.Interfaces;

public interface ISpeechSynthesizer
{
    string ProviderName { get; }
    bool IsSpeaking { get; }
    Task SpeakChunkAsync(string textChunk, double rate = 1.0, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
