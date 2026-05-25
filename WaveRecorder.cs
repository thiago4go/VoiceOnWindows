using System.Runtime.InteropServices;

namespace VoiceOnWindows;

internal sealed class WaveRecorder : IDisposable
{
    private const int WaveMapper = -1;
    private const int CallbackFunction = 0x00030000;
    private const int MmWimData = 0x3C0;
    private const int BufferCount = 4;
    private const int BufferBytes = 8192;
    private readonly object _gate = new();
    private readonly List<byte> _pcmBytes = [];
    private readonly List<WaveBuffer> _buffers = [];
    private WaveInProc? _callback;
    private IntPtr _handle;
    private bool _capturing;
    private bool _acceptingBuffers;
    private int _sampleRate;

    public void Start()
    {
        if (_capturing) return;

        _callback = WaveInCallback;
        Exception? lastError = null;

        foreach (int rate in new[] { 16000, 48000, 44100 })
        {
            var format = WaveFormat.Create(rate);
            uint result = waveInOpen(out _handle, WaveMapper, ref format, _callback, IntPtr.Zero, CallbackFunction);
            if (result == 0)
            {
                _sampleRate = rate;
                break;
            }

            lastError = new InvalidOperationException($"Could not open microphone at {rate} Hz. winmm error {result}.");
        }

        if (_handle == IntPtr.Zero)
        {
            throw lastError ?? new InvalidOperationException("Could not open microphone.");
        }

        _capturing = true;
        _acceptingBuffers = true;

        for (int i = 0; i < BufferCount; i++)
        {
            var buffer = new WaveBuffer(BufferBytes);
            _buffers.Add(buffer);
            Check(waveInPrepareHeader(_handle, buffer.HeaderPointer, Marshal.SizeOf<WaveHeader>()), "prepare microphone buffer");
            Check(waveInAddBuffer(_handle, buffer.HeaderPointer, Marshal.SizeOf<WaveHeader>()), "queue microphone buffer");
        }

        Check(waveInStart(_handle), "start microphone capture");
    }

    public byte[] StopToWav()
    {
        if (!_capturing) return [];

        _capturing = false;
        waveInStop(_handle);
        waveInReset(_handle);
        _acceptingBuffers = false;

        foreach (var buffer in _buffers)
        {
            waveInUnprepareHeader(_handle, buffer.HeaderPointer, Marshal.SizeOf<WaveHeader>());
        }

        waveInClose(_handle);
        _handle = IntPtr.Zero;

        byte[] pcm;
        lock (_gate)
        {
            pcm = _pcmBytes.ToArray();
            _pcmBytes.Clear();
        }

        return WavFile.FromPcm16(pcm, _sampleRate, channels: 1);
    }

    public void Dispose()
    {
        try
        {
            if (_handle != IntPtr.Zero)
            {
                StopToWav();
            }
        }
        catch
        {
            // Best effort cleanup.
        }

        foreach (var buffer in _buffers)
        {
            buffer.Dispose();
        }

        _buffers.Clear();
    }

    private void WaveInCallback(IntPtr hwi, uint message, IntPtr instance, IntPtr headerPointer, IntPtr reserved)
    {
        if (message != MmWimData || headerPointer == IntPtr.Zero) return;

        var header = Marshal.PtrToStructure<WaveHeader>(headerPointer);
        if (_acceptingBuffers && header.BytesRecorded > 0)
        {
            byte[] chunk = new byte[header.BytesRecorded];
            Marshal.Copy(header.Data, chunk, 0, chunk.Length);
            lock (_gate)
            {
                _pcmBytes.AddRange(chunk);
            }
        }

        if (_capturing)
        {
            waveInAddBuffer(hwi, headerPointer, Marshal.SizeOf<WaveHeader>());
        }
    }

    private static void Check(uint result, string action)
    {
        if (result != 0)
        {
            throw new InvalidOperationException($"Could not {action}. winmm error {result}.");
        }
    }

    private sealed class WaveBuffer : IDisposable
    {
        public IntPtr DataPointer { get; }
        public IntPtr HeaderPointer { get; }

        public WaveBuffer(int byteLength)
        {
            DataPointer = Marshal.AllocHGlobal(byteLength);
            HeaderPointer = Marshal.AllocHGlobal(Marshal.SizeOf<WaveHeader>());
            var header = new WaveHeader
            {
                Data = DataPointer,
                BufferLength = (uint)byteLength
            };
            Marshal.StructureToPtr(header, HeaderPointer, false);
        }

        public void Dispose()
        {
            if (HeaderPointer != IntPtr.Zero) Marshal.FreeHGlobal(HeaderPointer);
            if (DataPointer != IntPtr.Zero) Marshal.FreeHGlobal(DataPointer);
        }
    }

    private delegate void WaveInProc(IntPtr hwi, uint uMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2);

    [DllImport("winmm.dll")]
    private static extern uint waveInOpen(out IntPtr handle, int deviceId, ref WaveFormat format, WaveInProc callback, IntPtr instance, int flags);

    [DllImport("winmm.dll")]
    private static extern uint waveInPrepareHeader(IntPtr handle, IntPtr header, int headerSize);

    [DllImport("winmm.dll")]
    private static extern uint waveInAddBuffer(IntPtr handle, IntPtr header, int headerSize);

    [DllImport("winmm.dll")]
    private static extern uint waveInStart(IntPtr handle);

    [DllImport("winmm.dll")]
    private static extern uint waveInStop(IntPtr handle);

    [DllImport("winmm.dll")]
    private static extern uint waveInReset(IntPtr handle);

    [DllImport("winmm.dll")]
    private static extern uint waveInUnprepareHeader(IntPtr handle, IntPtr header, int headerSize);

    [DllImport("winmm.dll")]
    private static extern uint waveInClose(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveFormat
    {
        public ushort FormatTag;
        public ushort Channels;
        public uint SamplesPerSecond;
        public uint AverageBytesPerSecond;
        public ushort BlockAlign;
        public ushort BitsPerSample;
        public ushort Size;

        public static WaveFormat Create(int sampleRate)
        {
            const ushort channels = 1;
            const ushort bitsPerSample = 16;
            ushort blockAlign = (ushort)(channels * bitsPerSample / 8);
            return new WaveFormat
            {
                FormatTag = 1,
                Channels = channels,
                SamplesPerSecond = (uint)sampleRate,
                AverageBytesPerSecond = (uint)(sampleRate * blockAlign),
                BlockAlign = blockAlign,
                BitsPerSample = bitsPerSample,
                Size = 0
            };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public IntPtr Data;
        public uint BufferLength;
        public uint BytesRecorded;
        public IntPtr User;
        public uint Flags;
        public uint Loops;
        public IntPtr Next;
        public IntPtr Reserved;
    }
}
