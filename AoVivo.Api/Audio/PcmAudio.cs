using System.Buffers.Binary;

namespace AoVivo.Api.Audio;

/// <summary>
/// The audio format spoken with VoiceLive (same as the console app's AudioProcessor):
/// raw PCM, signed 16-bit little-endian, 24 kHz, mono. Also converts to/from WAV.
/// </summary>
public static class PcmAudio
{
    public const int SampleRate = 24000;
    public const int BitsPerSample = 16;
    public const int Channels = 1;
    public const int BlockAlign = Channels * BitsPerSample / 8;   // 2 bytes per sample frame
    public const int BytesPerSecond = SampleRate * BlockAlign;    // 48,000
    /// <summary>VoiceLive rejects committing less than 100 ms of audio.</summary>
    public const int MinBytes = BytesPerSecond / 10;

    private const int WavHeaderSize = 44;
    private const ushort WaveFormatPcm = 1;
    private const ushort WaveFormatExtensible = 0xFFFE;

    /// <summary>Returns why <paramref name="pcm"/> cannot be sent to the agent, or null when it is fine.</summary>
    public static string? Validate(byte[] pcm)
    {
        if (pcm.Length % BlockAlign != 0)
            return $"PCM16 audio must have an even number of bytes (got {pcm.Length}).";
        if (pcm.Length < MinBytes)
            return $"Audio is too short: at least 100 ms ({MinBytes} bytes of PCM16 24 kHz mono) is required.";
        return null;
    }

    public static bool IsWav(ReadOnlySpan<byte> data) =>
        data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WAVE"u8);

    /// <summary>Extracts the PCM samples from a WAV file, which must already be PCM16 / 24 kHz / mono.</summary>
    /// <exception cref="InvalidDataException">Not a readable WAV, or a different audio format.</exception>
    public static byte[] FromWav(byte[] wav)
    {
        if (!IsWav(wav))
            throw new InvalidDataException("Not a RIFF/WAVE file.");

        ReadOnlySpan<byte> data = wav;
        bool formatChecked = false;
        int position = 12;

        while (position + 8 <= data.Length)
        {
            ReadOnlySpan<byte> chunkId = data.Slice(position, 4);
            long chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(position + 4, 4));
            int body = position + 8;
            // Streaming writers may leave the size at 0 / 0xFFFFFFFF: clamp to what is actually there.
            int available = (int)Math.Min(chunkSize, data.Length - body);

            if (chunkId.SequenceEqual("fmt "u8))
            {
                if (available < 16)
                    throw new InvalidDataException("WAV 'fmt ' chunk is truncated.");
                ReadOnlySpan<byte> fmt = data.Slice(body, available);
                ushort formatTag = BinaryPrimitives.ReadUInt16LittleEndian(fmt);
                ushort channels = BinaryPrimitives.ReadUInt16LittleEndian(fmt[2..]);
                uint sampleRate = BinaryPrimitives.ReadUInt32LittleEndian(fmt[4..]);
                ushort bits = BinaryPrimitives.ReadUInt16LittleEndian(fmt[14..]);

                if ((formatTag != WaveFormatPcm && formatTag != WaveFormatExtensible)
                    || channels != Channels || sampleRate != SampleRate || bits != BitsPerSample)
                {
                    throw new InvalidDataException(
                        $"WAV must be PCM 16-bit, {SampleRate} Hz, mono (got format {formatTag}, {bits}-bit, {sampleRate} Hz, {channels} channel(s)). " +
                        $"Convert it with: ffmpeg -i input -ar {SampleRate} -ac 1 -c:a pcm_s16le output.wav");
                }
                formatChecked = true;
            }
            else if (chunkId.SequenceEqual("data"u8))
            {
                if (!formatChecked)
                    throw new InvalidDataException("WAV 'data' chunk appears before the 'fmt ' chunk.");
                return data.Slice(body, available).ToArray();
            }

            // Chunks are word-aligned: odd sizes are followed by one padding byte.
            position = body + available + (available % 2);
        }

        throw new InvalidDataException("WAV file has no 'data' chunk.");
    }

    /// <summary>Wraps PCM16 / 24 kHz / mono samples in a 44-byte WAV header.</summary>
    public static byte[] ToWav(byte[] pcm)
    {
        var wav = new byte[WavHeaderSize + pcm.Length];
        Span<byte> header = wav;

        "RIFF"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], (uint)(WavHeaderSize - 8 + pcm.Length));
        "WAVE"u8.CopyTo(header[8..]);

        "fmt "u8.CopyTo(header[12..]);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], 16);          // fmt chunk size
        BinaryPrimitives.WriteUInt16LittleEndian(header[20..], WaveFormatPcm);
        BinaryPrimitives.WriteUInt16LittleEndian(header[22..], Channels);
        BinaryPrimitives.WriteUInt32LittleEndian(header[24..], SampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(header[28..], BytesPerSecond);
        BinaryPrimitives.WriteUInt16LittleEndian(header[32..], BlockAlign);
        BinaryPrimitives.WriteUInt16LittleEndian(header[34..], BitsPerSample);

        "data"u8.CopyTo(header[36..]);
        BinaryPrimitives.WriteUInt32LittleEndian(header[40..], (uint)pcm.Length);

        pcm.CopyTo(wav.AsSpan(WavHeaderSize));
        return wav;
    }
}
