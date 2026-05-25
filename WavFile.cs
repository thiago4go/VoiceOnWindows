using System.Buffers.Binary;

namespace VoiceOnWindows;

internal static class WavFile
{
    public static byte[] FromPcm16(byte[] pcmBytes, int sampleRate, short channels)
    {
        const short bitsPerSample = 16;
        short blockAlign = (short)(channels * bitsPerSample / 8);
        int byteRate = sampleRate * blockAlign;
        byte[] wav = new byte[44 + pcmBytes.Length];

        WriteAscii(wav, 0, "RIFF");
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(4), 36 + pcmBytes.Length);
        WriteAscii(wav, 8, "WAVE");
        WriteAscii(wav, 12, "fmt ");
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(22), channels);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(24), sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(28), byteRate);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(32), blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(34), bitsPerSample);
        WriteAscii(wav, 36, "data");
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(40), pcmBytes.Length);
        Buffer.BlockCopy(pcmBytes, 0, wav, 44, pcmBytes.Length);
        return wav;
    }

    private static void WriteAscii(byte[] buffer, int offset, string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            buffer[offset + i] = (byte)value[i];
        }
    }
}
