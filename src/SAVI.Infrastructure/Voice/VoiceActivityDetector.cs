using System.Diagnostics;
using SAVI.Core.Interfaces;

namespace SAVI.Infrastructure.Voice;

public class VoiceActivityDetector : IVoiceActivityDetector
{
    private readonly object _lock = new();
    private readonly Stopwatch _silenceStopwatch = new();
    private readonly Stopwatch _speechOnsetStopwatch = new();
    private bool _isSpeechActive;
    private double _adaptiveNoiseFloor = 0.02;
    private double _currentRms = 0.0;
    private double _speechStartThreshold = 0.12;
    private double _speechContinuationThreshold = 0.08;
    private int _minSpeechDurationMs = 0;
    private int _silenceDurationThresholdMs = 700;
    private double _preRollDurationMs = 300.0;
    private double _postRollDurationMs = 150.0;
    private bool _isSpeakerAware = true;

    public bool IsSpeechActive
    {
        get { lock (_lock) return _isSpeechActive; }
        private set { lock (_lock) _isSpeechActive = value; }
    }

    public double AdaptiveNoiseFloor
    {
        get { lock (_lock) return _adaptiveNoiseFloor; }
        private set { lock (_lock) _adaptiveNoiseFloor = value; }
    }

    public double CurrentRms
    {
        get { lock (_lock) return _currentRms; }
        private set { lock (_lock) _currentRms = value; }
    }

    public double SpeechStartThreshold
    {
        get { lock (_lock) return _speechStartThreshold; }
        set { lock (_lock) _speechStartThreshold = value; }
    }

    public double SpeechContinuationThreshold
    {
        get { lock (_lock) return _speechContinuationThreshold; }
        set { lock (_lock) _speechContinuationThreshold = value; }
    }

    public int MinSpeechDurationMs
    {
        get { lock (_lock) return _minSpeechDurationMs; }
        set { lock (_lock) _minSpeechDurationMs = value; }
    }

    public int SilenceDurationThresholdMs
    {
        get { lock (_lock) return _silenceDurationThresholdMs; }
        set { lock (_lock) _silenceDurationThresholdMs = value; }
    }

    public double PreRollDurationMs
    {
        get { lock (_lock) return _preRollDurationMs; }
        set { lock (_lock) _preRollDurationMs = value; }
    }

    public double PostRollDurationMs
    {
        get { lock (_lock) return _postRollDurationMs; }
        set { lock (_lock) _postRollDurationMs = value; }
    }

    public bool IsSpeakerAware
    {
        get { lock (_lock) return _isSpeakerAware; }
        set { lock (_lock) _isSpeakerAware = value; }
    }

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
            _currentRms = level;

            // Adaptive noise floor tracking when speech is not active
            if (!_isSpeechActive)
            {
                _adaptiveNoiseFloor = (_adaptiveNoiseFloor * 0.95) + (level * 0.05);
            }

            double effectiveOnsetThreshold = Math.Max(_speechStartThreshold, _adaptiveNoiseFloor + 0.04);
            double effectiveContinuationThreshold = Math.Max(_speechContinuationThreshold, effectiveOnsetThreshold * 0.7);

            if (!_isSpeechActive)
            {
                if (level >= effectiveOnsetThreshold)
                {
                    if (_minSpeechDurationMs <= 0)
                    {
                        _isSpeechActive = true;
                        _speechOnsetStopwatch.Reset();
                        _silenceStopwatch.Reset();
                        triggerDetected = true;
                        triggerStarted = true;
                    }
                    else
                    {
                        if (!_speechOnsetStopwatch.IsRunning)
                        {
                            _speechOnsetStopwatch.Restart();
                        }
                        else if (_speechOnsetStopwatch.ElapsedMilliseconds >= _minSpeechDurationMs)
                        {
                            _isSpeechActive = true;
                            _speechOnsetStopwatch.Reset();
                            _silenceStopwatch.Reset();
                            triggerDetected = true;
                            triggerStarted = true;
                        }
                    }
                }
                else
                {
                    _speechOnsetStopwatch.Reset();
                }
            }
            else // speech is active
            {
                if (level >= effectiveContinuationThreshold)
                {
                    _silenceStopwatch.Reset();
                    triggerDetected = true;
                    triggerContinued = true;
                }
                else
                {
                    if (!_silenceStopwatch.IsRunning)
                    {
                        _silenceStopwatch.Restart();
                    }
                    else if (_silenceStopwatch.ElapsedMilliseconds >= _silenceDurationThresholdMs)
                    {
                        _isSpeechActive = false;
                        _silenceStopwatch.Reset();
                        _speechOnsetStopwatch.Reset();
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
            _speechOnsetStopwatch.Reset();
            _silenceStopwatch.Reset();
            _isSpeechActive = true;
        }
        SpeechStarted?.Invoke();
    }

    public void NotifySpeechStopped()
    {
        lock (_lock)
        {
            _speechOnsetStopwatch.Reset();
            _silenceStopwatch.Reset();
            _isSpeechActive = false;
        }
        SpeechStopped?.Invoke();
    }

    public void Reset()
    {
        lock (_lock)
        {
            _isSpeechActive = false;
            _silenceStopwatch.Reset();
            _speechOnsetStopwatch.Reset();
            _adaptiveNoiseFloor = 0.02;
            _currentRms = 0.0;
        }
    }
}
