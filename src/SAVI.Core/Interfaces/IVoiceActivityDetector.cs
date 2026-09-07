namespace SAVI.Core.Interfaces;

public interface IVoiceActivityDetector
{
    bool IsSpeechActive { get; }
    double SpeechStartThreshold { get; set; }
    int SilenceDurationThresholdMs { get; set; }

    void ProcessAudioFrame(double level);
    void NotifySpeechStarted();
    void NotifySpeechStopped();

    event Action? SpeechStarted;
    event Action<double>? SpeechDetected;
    event Action<double>? SpeechContinued;
    event Action? SpeechStopped;
}
