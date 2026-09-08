namespace SAVI.Core.Enums;

public enum VoiceState
{
    Idle = 0,
    Listening = 1,
    Processing = 2,
    Searching = 3,
    Executing = 4,
    Speaking = 5,
    Error = 6,
    DetectingSpeech = 7,
    Interrupted = 8,
    Stopping = 9,
    RequestingPermission = 10,
    Starting = 11,
    UserSpeaking = 12,
    Recovering = 13,
    Paused = 14
}
