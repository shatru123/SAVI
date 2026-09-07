namespace SAVI.Core.Interfaces;

public interface IStreamingTextToSpeech
{
    string ProviderName { get; }
    bool IsAvailable { get; }
    bool IsSpeaking { get; }

    Task StartAsync(string turnId, CancellationToken cancellationToken = default);
    Task WriteChunkAsync(string turnId, string textChunk, CancellationToken cancellationToken = default);
    Task PauseAsync(CancellationToken cancellationToken = default);
    Task ResumeAsync(CancellationToken cancellationToken = default);
    Task CancelAsync(string turnId, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);

    event Action<string>? ChunkStarted;
    event Action<string>? ChunkCompleted;
    event Action? PlaybackCompleted;
    event Action<string>? PlaybackError;
}
