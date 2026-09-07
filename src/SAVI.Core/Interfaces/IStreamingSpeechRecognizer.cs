namespace SAVI.Core.Interfaces;

public interface IStreamingSpeechRecognizer
{
    string ProviderName { get; }
    bool IsAvailable { get; }

    Task StartStreamingAsync(string turnId, Func<string, bool, Task> onTranscriptReceived, CancellationToken cancellationToken = default);
    Task StopStreamingAsync(string turnId, CancellationToken cancellationToken = default);
}
