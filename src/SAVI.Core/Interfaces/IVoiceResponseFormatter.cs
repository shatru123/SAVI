namespace SAVI.Core.Interfaces;

public interface IVoiceResponseFormatter
{
    string FormatForSpeech(string rawResponse, string? capability = null);
    IReadOnlyList<string> ChunkForStreamingTts(string speechText);
}
