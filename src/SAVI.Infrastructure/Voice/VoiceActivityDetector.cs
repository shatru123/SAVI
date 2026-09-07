using System.Diagnostics;
using SAVI.Core.Interfaces;

namespace SAVI.Infrastructure.Voice;

public class VoiceActivityDetector : IVoiceActivityDetector
{
    private readonly object _lock = new();
    private readonly Stopwatch _silenceStopwatch = new();
    private bool _isSpeechActive;

    public bool IsSpeechActive
    {
        get { lock (_lock) return _isSpeechActive; }
        private set { lock (_lock) _isSpeechActive = value; }
    }

    public double SpeechStartThreshold { get; set; } = 0.12;
    public int SilenceDurationThresholdMs { get; set; } = 800;

    public event Action? SpeechStarted;
    public event Action<double>? SpeechDetected;
    public event Action<double>? SpeechContinued;
    public event Action? SpeechStopped;

    public void ProcessAudioFrame(double level)
    {
        bool triggerStarted = false;
        bool triggerStopped = false;
        bool triggerContinued = false;
        bool triggerDetected = false;

        lock (_lock)
        {
            if (level >= SpeechStartThreshold)
            {
                triggerDetected = true;
                _silenceStopwatch.Reset();
                if (!_isSpeechActive)
                {
                    _isSpeechActive = true;
                    triggerStarted = true;
                }
                else
                {
                    triggerContinued = true;
                }
            }
            else
            {
                if (_isSpeechActive)
                {
                    if (!_silenceStopwatch.IsRunning)
                    {
                        _silenceStopwatch.Start();
                    }
                    else if (_silenceStopwatch.ElapsedMilliseconds >= SilenceDurationThresholdMs)
                    {
                        _isSpeechActive = false;
                        _silenceStopwatch.Reset();
                        triggerStopped = true;
                    }
                }
            }
        }

        if (triggerDetected)
        {
            SpeechDetected?.Invoke(level);
        }

        if (triggerStarted)
        {
            SpeechStarted?.Invoke();
        }
        else if (triggerContinued)
        {
            SpeechContinued?.Invoke(level);
        }
        else if (triggerStopped)
        {
            SpeechStopped?.Invoke();
        }
    }

    public void NotifySpeechStarted()
    {
        lock (_lock)
        {
            _silenceStopwatch.Reset();
            _isSpeechActive = true;
        }
        SpeechStarted?.Invoke();
    }

    public void NotifySpeechStopped()
    {
        lock (_lock)
        {
            _silenceStopwatch.Reset();
            _isSpeechActive = false;
        }
        SpeechStopped?.Invoke();
    }
}
