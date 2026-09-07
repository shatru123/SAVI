namespace SAVI.Core.Interfaces;

public interface IVoiceActivityDetector
{
    bool IsSpeechActive { get; }
    double AdaptiveNoiseFloor { get; }
    double CurrentRms { get; }
    double SpeechStartThreshold { get; set; }
    double SpeechContinuationThreshold { get; set; }
    int MinSpeechDurationMs { get; set; }
    int SilenceDurationThresholdMs { get; set; }
    double PreRollDurationMs { get; set; }
    double PostRollDurationMs { get; set; }
    bool IsSpeakerAware { get; set; }

    void ProcessAudioFrame(double level);
    void NotifySpeechStarted();
    void NotifySpeechStopped();
    void Reset();

    event Action? SpeechStarted;
    event Action<double>? SpeechDetected;
    event Action<double>? SpeechContinued;
    event Action? SpeechStopped;
}
