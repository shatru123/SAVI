namespace SAVI.Core.Interfaces;

public interface IAudioRingBuffer
{
    int CapacityBytes { get; }
    int AvailableBytes { get; }

    void Write(ReadOnlySpan<byte> data);
    void Write(ReadOnlySpan<float> samples);
    byte[] ReadAvailable();
    float[] ReadPreRoll(int durationMs, int sampleRate = 16000, int channels = 1);
    void Clear();
}
