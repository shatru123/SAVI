namespace SAVI.Core.Interfaces;

public interface ISpeechRecognizer
{
    string ProviderName { get; }
    bool IsSupported { get; }
    Task<bool> StartContinuousRecognitionAsync(Func<string, bool, Task> onTranscription, CancellationToken cancellationToken = default);
    Task StopRecognitionAsync(CancellationToken cancellationToken = default);
}
