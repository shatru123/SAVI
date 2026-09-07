using System;
using SAVI.Core.Interfaces;

namespace SAVI.Infrastructure.Voice;

public class AudioRingBuffer : IAudioRingBuffer
{
    private readonly byte[] _byteBuffer;
    private readonly object _lock = new();
    private int _writeOffset;
    private int _count;

    public int CapacityBytes => _byteBuffer.Length;
    public int AvailableBytes
    {
        get
        {
            lock (_lock) return _count;
        }
    }

    /// <summary>
    /// Creates an audio ring buffer. Default capacity is 64 KB (~2 seconds of 16kHz 16-bit mono PCM).
    /// </summary>
    public AudioRingBuffer(int capacityBytes = 64 * 1024)
    {
        if (capacityBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacityBytes), "Capacity must be greater than zero.");

        _byteBuffer = new byte[capacityBytes];
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty) return;

        lock (_lock)
        {
            var len = data.Length;
            if (len >= _byteBuffer.Length)
            {
                // If writing more than capacity, keep only the most recent chunk
                data.Slice(len - _byteBuffer.Length).CopyTo(_byteBuffer);
                _writeOffset = 0;
                _count = _byteBuffer.Length;
                return;
            }

            for (var i = 0; i < len; i++)
            {
                _byteBuffer[_writeOffset] = data[i];
                _writeOffset = (_writeOffset + 1) % _byteBuffer.Length;
            }

            _count = Math.Min(_byteBuffer.Length, _count + len);
        }
    }

    public void Write(ReadOnlySpan<float> samples)
    {
        if (samples.IsEmpty) return;

        // Convert float [-1.0, 1.0] to 16-bit PCM bytes (2 bytes per sample)
        var byteCount = samples.Length * 2;
        Span<byte> bytes = byteCount <= 4096 ? stackalloc byte[byteCount] : new byte[byteCount];

        for (var i = 0; i < samples.Length; i++)
        {
            var clamped = Math.Clamp(samples[i], -1.0f, 1.0f);
            var pcmSample = (short)(clamped * 32767.0f);
            var byteIndex = i * 2;
            bytes[byteIndex] = (byte)(pcmSample & 0xFF);
            bytes[byteIndex + 1] = (byte)((pcmSample >> 8) & 0xFF);
        }

        Write(bytes);
    }

    public byte[] ReadAvailable()
    {
        lock (_lock)
        {
            if (_count == 0) return Array.Empty<byte>();

            var result = new byte[_count];
            var start = (_writeOffset - _count + _byteBuffer.Length) % _byteBuffer.Length;

            for (var i = 0; i < _count; i++)
            {
                result[i] = _byteBuffer[(start + i) % _byteBuffer.Length];
            }

            return result;
        }
    }

    public float[] ReadPreRoll(int durationMs, int sampleRate = 16000, int channels = 1)
    {
        if (durationMs <= 0 || sampleRate <= 0 || channels <= 0)
            return Array.Empty<float>();

        lock (_lock)
        {
            var totalSamplesRequested = (int)((long)durationMs * sampleRate * channels / 1000);
            var bytesRequested = totalSamplesRequested * 2;
            var availableSamples = _count / 2;

            var samplesToRead = Math.Min(totalSamplesRequested, availableSamples);
            if (samplesToRead <= 0) return Array.Empty<float>();

            var bytesToRead = samplesToRead * 2;
            var start = (_writeOffset - bytesToRead + _byteBuffer.Length) % _byteBuffer.Length;

            var result = new float[samplesToRead];
            for (var i = 0; i < samplesToRead; i++)
            {
                var idx1 = (start + i * 2) % _byteBuffer.Length;
                var idx2 = (start + i * 2 + 1) % _byteBuffer.Length;
                var pcm = (short)(_byteBuffer[idx1] | (_byteBuffer[idx2] << 8));
                result[i] = pcm / 32767.0f;
            }

            return result;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _writeOffset = 0;
            _count = 0;
            Array.Clear(_byteBuffer, 0, _byteBuffer.Length);
        }
    }
}
